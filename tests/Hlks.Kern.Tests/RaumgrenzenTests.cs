using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Hilfen der Raumgrenzen-Tests (Modul 1c, B1). Alle Kennwerte (U, uw) sind frei gewählte Testwerte, keine Normwerte.</summary>
static class RaumgrenzenHilfe
{
    public static readonly Katalog Kat = ArchitekturHilfe.Kat;

    public static BefehlsErgebnis Ok(BefehlsErgebnis e) => ArchitekturHilfe.Ok(e);

    public static JsonArray Pts(params double[][] p) => ArchitekturHilfe.Pts(p);

    public static Projekt Leer()
    {
        var p = Projekt.Anlegen(Kat, null, "Test Raumgrenzen", "T", "pc");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" }));
        return p;
    }

    public static void Geschoss(Projekt p, string kz, double kote, double hoehe) =>
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = kz, ["teilVon"] = "GEB",
            ["eigenschaften"] = new JsonObject { ["kote"] = kote, ["hoehe"] = hoehe },
        }));

    /// <summary>Geschlossener Wandzug um die Achsen (x0, y0) … (x1, y1): erste Wand Süd (→ +x), dann Ost, Nord, West.</summary>
    public static List<string> Waende(Projekt p, string geschoss, double x0, double y0, double x1, double y1, double dicke, double? hoehe = null)
    {
        var par = new JsonObject
        {
            ["geschoss"] = geschoss, ["punkte"] = Pts([x0, y0], [x1, y0], [x1, y1], [x0, y1]), ["dicke"] = dicke, ["geschlossen"] = true,
        };
        if (hoehe is double h) par["hoehe"] = h;
        var e = Ok(p.Ausfuehren("wand_zeichnen", par));
        return e.Aenderungen.Where(a => a.Klasse == "Wand").Select(a => a.Kennzeichen).ToList();
    }

    public static string Wand(Projekt p, string geschoss, double[] a, double[] b, double dicke)
    {
        var e = Ok(p.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = geschoss, ["punkte"] = Pts(a, b), ["dicke"] = dicke }));
        return e.Aenderungen.Single(x => x.Klasse == "Wand").Kennzeichen;
    }

    public static string Raum(Projekt p, string geschoss, double x, double y, double? lichteHoehe = null, string? nummer = null)
    {
        var par = new JsonObject { ["geschoss"] = geschoss, ["punkt"] = new JsonArray(x, y) };
        if (nummer is not null) par["nummer"] = nummer;
        var e = Ok(p.Ausfuehren("raum_erkennen", par));
        var kz = e.Aenderungen.Single(a => a.Klasse == "Raum").Kennzeichen;
        if (lichteHoehe is double h) Eigenschaften(p, kz, new JsonObject { ["lichteHoehe"] = h });
        return kz;
    }

    public static void Eigenschaften(Projekt p, string element, JsonObject eig) =>
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = element, ["eigenschaften"] = eig }));

    public static string Fenster(Projekt p, string wand, double abstand, double breite, double hoehe, double bruestung)
    {
        var e = Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject
        {
            ["klasse"] = "Fenster", ["wand"] = wand, ["abstand"] = abstand, ["breite"] = breite, ["hoehe"] = hoehe, ["bruestung"] = bruestung,
        }));
        return e.Aenderungen.Single(a => a.Klasse == "Fenster").Kennzeichen;
    }

    public static string Tuer(Projekt p, string wand, double abstand, double breite, double hoehe)
    {
        var e = Ok(p.Ausfuehren("oeffnung_setzen", new JsonObject { ["klasse"] = "Tuer", ["wand"] = wand, ["abstand"] = abstand, ["breite"] = breite, ["hoehe"] = hoehe }));
        return e.Aenderungen.Single(a => a.Klasse == "Tuer").Kennzeichen;
    }

    /// <summary>Platte (Decke oder Dach) als Rechteck, z = Oberkante, Dicke nach unten.</summary>
    public static void Platte(Projekt p, string klasse, string kz, string geschoss, double x0, double y0, double x1, double y1, double ok, double dicke, string? art = null)
    {
        var par = new JsonObject
        {
            ["klasse"] = klasse, ["kennzeichen"] = kz, ["geschoss"] = geschoss,
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([x0, y0, ok], [x1, y0, ok], [x1, y1, ok], [x0, y1, ok]), ["dicke"] = dicke },
        };
        if (art is not null) par["eigenschaften"] = new JsonObject { ["art"] = art };
        Ok(p.Ausfuehren("element_anlegen", par));
    }

    public static Element E(Projekt p, string kz) => ArchitekturHilfe.Hole(p, kz);

    public static RaumgrenzenErgebnis Rechnen(Projekt p, params string[] raeume) =>
        Raumgrenzen.Rechnen(p.Modell, p.Katalog, raeume.Length == 0 ? null : raeume.Select(r => E(p, r)));

    /// <summary>Summe der Flächen (m²) der Raumgrenzen, die die Bedingung erfüllen.</summary>
    public static double Summe(IEnumerable<Raumgrenze> g, Func<Raumgrenze, bool> wo) => g.Where(wo).Sum(x => x.Flaeche);

    public static void Nahe(double erwartet, double ist, double tol = 1e-6, string? text = null) =>
        Assert.True(Math.Abs(erwartet - ist) <= tol, $"{text}: erwartet {erwartet}, ist {ist}");
}

/// <summary>Handrechnungen zum Rechenkern der Raumgrenzen (entwurf/modul-1c.md, Abschnitt «Raumgrenzen»).</summary>
public class RaumgrenzenTests
{
    static Projekt Ok1(out string raum) => Rechteckprojekt(out raum, true);

    /// <summary>EG (Kote 0, Höhe 3000), OG (Kote 3000, Höhe 3000). Wände Dicke 200 um die Achsen (−100, −100) … (6100, 4100):
    /// Raum innen 6.0 × 4.0 m, lichte Höhe 2.6 m (Testwert). Fenster in W-1 (Süd) 1.2 × 1.2 m, Brüstung 0.9 m.
    /// mitOg: Raum darüber im OG gleich gross, Decke D-1 (OK 3000, 250 dick) und Bodenplatte D-0 (OK 0, 300 dick).</summary>
    static Projekt Rechteckprojekt(out string raum, bool mitOg)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Geschoss(p, "OG", 3000, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);
        raum = RaumgrenzenHilfe.Raum(p, "EG", 3000, 2000, 2600, "0.01");
        RaumgrenzenHilfe.Fenster(p, "W-1", 3100, 1200, 1200, 900);
        if (mitOg)
        {
            RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Raum", ["kennzeichen"] = "R-OG", ["geschoss"] = "OG",
                ["eigenschaften"] = new JsonObject { ["nummer"] = "1.01", ["lichteHoehe"] = 2600 },
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]) },
            }));
            RaumgrenzenHilfe.Platte(p, "Decke", "D-1", "OG", -200, -200, 6200, 4200, 3000, 250);
            RaumgrenzenHilfe.Platte(p, "Decke", "D-0", "EG", -200, -200, 6200, 4200, 0, 300, "boden");
        }
        return p;
    }

    static void UWerteSetzen(Projekt p)
    {
        // Testwerte, frei gewählt – keine Normwerte
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Aufbau", ["kennzeichen"] = "AUF-W",
            ["eigenschaften"] = new JsonObject { ["art"] = "wand", ["uWert"] = new JsonObject { ["wert"] = 0.25, ["quelle"] = "Testwert, frei gewählt" } },
        }));
        foreach (var w in new[] { "W-1", "W-2", "W-3", "W-4" })
            RaumgrenzenHilfe.Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = w, ["bezuege"] = new JsonObject { ["aufbau"] = "AUF-W" } }));
        RaumgrenzenHilfe.Eigenschaften(p, "F-1", new JsonObject { ["uw"] = new JsonObject { ["wert"] = 1.0, ["quelle"] = "Testwert, frei gewählt" } });
    }

    // ------------------------------------------------------------------ 1) Rechteckraum mit vier Aussenwänden

    [Fact]
    public void Rechteckraum_AussenwaendeFensterBodenErdreichDeckeGegenRaumDarueber()
    {
        var p = Ok1(out var raum);
        UWerteSetzen(p);
        var erg = RaumgrenzenHilfe.Rechnen(p, raum);
        var g = erg.Grenzen;
        Assert.All(g, x => Assert.Equal(raum, x.Raum.Kennzeichen));

        // Handrechnung: Höhe 2.6 m; Süd 6.0 × 2.6 − 1.2 × 1.2 = 14.16 m², Ost/West 4.0 × 2.6 = 10.4 m², Nord 15.6 m²
        Raumgrenze Wand(string kz) => g.Single(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == kz);
        RaumgrenzenHilfe.Nahe(14.16, Wand("W-1").Flaeche, 1e-9, "Süd");
        RaumgrenzenHilfe.Nahe(10.4, Wand("W-2").Flaeche, 1e-9, "Ost");
        RaumgrenzenHilfe.Nahe(15.6, Wand("W-3").Flaeche, 1e-9, "Nord");
        RaumgrenzenHilfe.Nahe(10.4, Wand("W-4").Flaeche, 1e-9, "West");
        Assert.All(g.Where(x => x.Art == "wand"), x => { Assert.Equal("aussen", x.Angrenzung); Assert.Equal("aussen", x.Nachbar); Assert.Equal(90, x.Neigung); Assert.False(x.Offen); });

        // Orientierung (nordwinkel 0): Süd 180°, Ost 90°, Nord 0°, West 270°
        Assert.Equal(180, Wand("W-1").Azimut);
        Assert.Equal(90, Wand("W-2").Azimut);
        Assert.Equal(0, Wand("W-3").Azimut);
        Assert.Equal(270, Wand("W-4").Azimut);

        // Fenster als eigene Raumgrenze, U·A mit den Testwerten
        var f = g.Single(x => x.Art == "fenster");
        Assert.Equal("F-1", f.Bauteil!.Kennzeichen);
        RaumgrenzenHilfe.Nahe(1.44, f.Flaeche, 1e-9, "Fenster");
        Assert.Equal(180, f.Azimut);
        Assert.Equal(1.0, f.U);
        RaumgrenzenHilfe.Nahe(1.44, f.UA!.Value, 1e-9);
        Assert.Equal(0.25, Wand("W-1").U);
        Assert.Equal("AUF-W", Wand("W-1").Aufbau!.Kennzeichen);
        RaumgrenzenHilfe.Nahe(0.25 * 14.16, Wand("W-1").UA!.Value, 1e-9);

        // Boden: EG ist unterstes Geschoss → Erdreich, Bauteil = Bodenplatte D-0; Decke gegen R-OG, Bauteil D-1
        var boden = g.Single(x => x.Art == "boden");
        Assert.Equal("erdreich", boden.Angrenzung);
        Assert.Equal("D-0", boden.Bauteil?.Kennzeichen);
        RaumgrenzenHilfe.Nahe(24.0, boden.Flaeche, 1e-9, "Boden");
        Assert.Equal(180, boden.Neigung);
        Assert.Null(boden.Azimut);
        var decke = g.Single(x => x.Art == "decke");
        Assert.Equal("R-OG", decke.NachbarRaum!.Kennzeichen);
        Assert.Equal("beheizt", decke.Angrenzung);
        Assert.Equal("D-1", decke.Bauteil?.Kennzeichen);
        RaumgrenzenHilfe.Nahe(24.0, decke.Flaeche, 1e-9, "Decke");
        Assert.Equal(0, decke.Neigung);
        Assert.Equal(7, g.Count);   // 4 Wände, 1 Fenster, Boden, Decke

        // Summen: Seiten 2 × (6 + 4) × 2.6 = 52 m² aussen, Boden 24 m² Erdreich, Decke 24 m² beheizt
        RaumgrenzenHilfe.Nahe(52.0, RaumgrenzenHilfe.Summe(g, x => x.Angrenzung == "aussen"), 1e-9);
        RaumgrenzenHilfe.Nahe(24.0, RaumgrenzenHilfe.Summe(g, x => x.Angrenzung == "erdreich"), 1e-9);
        var rm = Assert.Single(erg.Raeume);
        RaumgrenzenHilfe.Nahe(24.0, rm.FlaecheM2, 1e-9);
        Assert.Equal(2600, rm.HoeheMm);
    }

    [Fact]
    public void Rechteckraum_RaumDarueberSiehtDenBodenGegenDenRaumDarunterUndDasDach()
    {
        var p = Ok1(out var raum);
        var g = RaumgrenzenHilfe.Rechnen(p, "R-OG").Grenzen;
        var boden = g.Single(x => x.Art == "boden");
        Assert.Equal(raum, boden.NachbarRaum!.Kennzeichen);
        Assert.Equal("D-1", boden.Bauteil?.Kennzeichen);
        RaumgrenzenHilfe.Nahe(24.0, boden.Flaeche, 1e-9);
        var dach = g.Single(x => x.Art == "dach");   // Rest oben: aussen (Dach), keine Platte darüber
        Assert.Equal("aussen", dach.Angrenzung);
        Assert.Null(dach.Bauteil);
        // im OG gibt es keine Wände: vier offene Kanten gegen aussen, je mit Hinweis
        Assert.Equal(4, g.Count(x => x.Offen));
        RaumgrenzenHilfe.Nahe(2 * (6 + 4) * 2.6, RaumgrenzenHilfe.Summe(g, x => x.Offen), 1e-9);
    }

    [Fact]
    public void Rechteckraum_AbfrageUndListeUeberDieTuer()
    {
        var p = Ok1(out var raum);
        UWerteSetzen(p);
        var o = (JsonObject)Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["raum"] = raum });
        Assert.Equal("Raumgrenzen", o["ableitung"]!.GetValue<string>());
        Assert.Equal(7, o["zeilen"]!.AsArray().Count);
        var s = o["summenJeRaum"]!.AsArray().Single()!;
        Assert.Equal(52.0, s["jeAngrenzung"]!["aussen"]!["flaeche_m2"]!.GetValue<double>());
        Assert.Equal(24.0, s["jeAngrenzung"]!["erdreich"]!["flaeche_m2"]!.GetValue<double>());
        // U·A aussen: 0.25 × (14.16 + 10.4 + 15.6 + 10.4) + 1.0 × 1.44 = 14.08 W/K
        Assert.Equal(14.08, s["jeAngrenzung"]!["aussen"]!["ua_WK"]!.GetValue<double>());
        var fenster = o["zeilen"]!.AsArray().Single(z => z!["art"]!.GetValue<string>() == "fenster")!;
        Assert.Equal(1.44, fenster["flaeche_m2"]!.GetValue<double>());
        Assert.Equal("F-1.uw", fenster["uQuelle"]!.GetValue<string>());
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().Contains("ohne Normfaktoren"));
        // Geschossfilter und Fehler
        Assert.Equal(1, ((JsonObject)Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["geschoss"] = "EG" }))["anzahlRaeume"]!.GetValue<int>());
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["raum"] = raum, ["geschoss"] = "OG" }));
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["raum"] = "W-1" }));
    }

    [Fact]
    public void Geometrie_LaengeMalHoeheGleichFlaeche_RingGleichFlaeche_AbfrageMitGeometrie()
    {
        var p = Ok1(out var raum);
        var g = RaumgrenzenHilfe.Rechnen(p, raum).Grenzen;
        foreach (var x in g.Where(x => x.Art is "wand" or "fenster" or "tuer"))
        {
            Assert.NotNull(x.Von); Assert.NotNull(x.Bis);
            double brutto = x.Laenge / 1000 * (x.Oben - x.Unten) / 1000;
            double oeffnungen = x.Art == "wand" ? g.Where(o => o.Art is "fenster" or "tuer" && o.Bauteil?.Bezuege.GetValueOrDefault("wand") == x.Bauteil?.Id).Sum(o => o.Flaeche) : 0;
            RaumgrenzenHilfe.Nahe(brutto - oeffnungen, x.Flaeche, 1e-9, $"{x.Art} {x.Bauteil?.Kennzeichen}");
        }
        var sued = g.Single(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == "W-1");
        ArchitekturHilfe.Nahe([0, 0], sued.Von!);
        ArchitekturHilfe.Nahe([6000, 0], sued.Bis!);
        Assert.Equal(0, sued.Unten);
        Assert.Equal(2600, sued.Oben);
        var f = g.Single(x => x.Art == "fenster");   // 1.2 m breit bei x 2400 … 3600, Brüstung 0.9 m, 1.2 m hoch
        ArchitekturHilfe.Nahe([2400, 0], f.Von!);
        ArchitekturHilfe.Nahe([3600, 0], f.Bis!);
        Assert.Equal(900, f.Unten);
        Assert.Equal(2100, f.Oben);
        foreach (var x in g.Where(x => x.Art is "boden" or "decke"))
        {
            Assert.NotNull(x.Ring);
            RaumgrenzenHilfe.Nahe(x.Flaeche * 1e6, Math.Abs(Polygone.Flaecheninhalt(x.Ring!)), 1e-3, x.Art);
            Assert.Equal(x.Art == "boden" ? 0 : 2600, x.Unten);
        }
        // Abfrage: mit geometrie true Punkte bzw. Polygon, sonst nicht
        var mit = (JsonObject)Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["raum"] = raum, ["geometrie"] = true });
        var zeilen = mit["zeilen"]!.AsArray();
        var w = zeilen.Single(z => z!["bauteil"]?.GetValue<string>() == "W-1")!;
        Assert.Equal(2, w["punkte"]!.AsArray().Count);
        Assert.Equal(6000, w["punkte"]![1]![0]!.GetValue<double>());
        Assert.Equal(2600, w["oben"]!.GetValue<double>());
        var boden = zeilen.Single(z => z!["art"]!.GetValue<string>() == "boden")!;
        Assert.Equal(4, boden["polygon"]!.AsArray().Count);
        Assert.Equal(0, boden["z"]!.GetValue<double>());
        var ohne = (JsonObject)Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["raum"] = raum });
        Assert.All(ohne["zeilen"]!.AsArray(), z => { Assert.Null(z!["punkte"]); Assert.Null(z["polygon"]); });
    }

    [Fact]
    public void JeGeschoss_FuerDieGrafik_ImZwischenspeicherEinmal_HinweiseJeAufruf()
    {
        var p = Ok1(out var raum);
        var m = p.Modell; var k = p.Katalog;
        string eg = RaumgrenzenHilfe.E(p, "EG").Id, og = RaumgrenzenHilfe.E(p, "OG").Id;
        using (Wandgeometrie.Zwischenspeichern(m, k))
        {
            var a = Raumgrenzen.JeGeschoss(m, k, eg);
            Assert.Same(a, Raumgrenzen.JeGeschoss(m, k, eg));
            Assert.Equal(7, a.Grenzen.Count);
            Assert.All(a.Grenzen, x => Assert.Equal(raum, x.Raum.Kennzeichen));
            var o = Raumgrenzen.JeGeschoss(m, k, og);   // derselbe vorbereitete Lauf, eigene Hinweise
            Assert.All(o.Grenzen, x => Assert.Equal("R-OG", x.Raum.Kennzeichen));
            Assert.Contains(o.Hinweise, h => h.Contains("ohne Wand"));
            Assert.DoesNotContain(a.Hinweise, h => h.Contains("ohne Wand"));
        }
        Assert.NotSame(Raumgrenzen.JeGeschoss(m, k, eg), Raumgrenzen.JeGeschoss(m, k, eg));   // ohne Zwischenspeicher neu
    }

    [Fact]
    public void Huellbauteile_InnenUndAussenmass()
    {
        var p = Ok1(out _);
        UWerteSetzen(p);
        var erg = Raumgrenzen.Rechnen(p.Modell, p.Katalog);
        var liste = RaumgrenzenAbfragen.HuellbauteileAus(p.Modell, p.Katalog, erg, null);
        var w1 = liste.Single(x => x.Bauteil.Kennzeichen == "W-1");
        // innen 14.16 m²; aussen: Achse 6.2 m + 2 × 0.1 m Verlängerung = 6.4 m × Wandhöhe 3.0 m − 1.44 m² = 17.76 m²
        RaumgrenzenHilfe.Nahe(14.16, w1.FlaecheInnen, 1e-9);
        RaumgrenzenHilfe.Nahe(17.76, w1.FlaecheAussen!.Value, 1e-9);
        Assert.Equal(180, w1.Azimut);
        Assert.Equal("aussen", w1.Angrenzung);
        Assert.Equal(0.25, w1.U);
        var f1 = liste.Single(x => x.Bauteil.Kennzeichen == "F-1");
        RaumgrenzenHilfe.Nahe(1.44, f1.FlaecheAussen!.Value, 1e-9);
        // Bodenplatte D-0 gegen Erdreich: innen 24 m², aussen Polygon 6.4 × 4.4 = 28.16 m²
        var d0 = liste.Single(x => x.Bauteil.Kennzeichen == "D-0");
        RaumgrenzenHilfe.Nahe(24.0, d0.FlaecheInnen, 1e-9);
        RaumgrenzenHilfe.Nahe(28.16, d0.FlaecheAussen!.Value, 1e-9);
        Assert.Equal("erdreich", d0.Angrenzung);
        Assert.Equal(180, d0.Neigung);
        // Decke D-1 liegt zwischen zwei beheizten Räumen: keine Hülle
        Assert.DoesNotContain(liste, x => x.Bauteil.Kennzeichen == "D-1");
        var o = (JsonObject)Tuer.Abfrage(p, "huellbauteile", new JsonObject { ["geschoss"] = "EG" });
        Assert.Equal("Hüllbauteile", o["ableitung"]!.GetValue<string>());
        Assert.Contains(o["zeilen"]!.AsArray(), z => z!["bauteil"]!.GetValue<string>() == "W-3" && z["flaecheAussen_m2"]!.GetValue<double>() == 19.2);
    }

    // ------------------------------------------------------------------ 2) Zwei Räume mit Innenwand

    /// <summary>Aussenwände um die Achsen (−100, −100) … (8100, 4100), Dicke 200; Innenwand Achse x = 4050, Dicke 100.
    /// R-1 innen 0 … 4000, R-2 innen 4100 … 8000 (je 4.0 m tief); Höhe = Geschosshöhe 3.0 m.</summary>
    static Projekt ZweiRaeume(out string r1, out string r2)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 8100, 4100, 200);
        RaumgrenzenHilfe.Wand(p, "EG", [4050, 0], [4050, 4000], 100);
        r1 = RaumgrenzenHilfe.Raum(p, "EG", 2000, 2000);
        r2 = RaumgrenzenHilfe.Raum(p, "EG", 6000, 2000);
        return p;
    }

    [Fact]
    public void ZweiRaeume_InnenwandNachbarGegenseitig()
    {
        var p = ZweiRaeume(out var r1, out var r2);
        var g = RaumgrenzenHilfe.Rechnen(p).Grenzen;
        var a = g.Single(x => x.Raum.Kennzeichen == r1 && x.Bauteil?.Kennzeichen == "W-5");
        var b = g.Single(x => x.Raum.Kennzeichen == r2 && x.Bauteil?.Kennzeichen == "W-5");
        Assert.Equal(r2, a.NachbarRaum!.Kennzeichen);
        Assert.Equal(r1, b.NachbarRaum!.Kennzeichen);
        Assert.Equal("beheizt", a.Angrenzung);
        Assert.Equal("beheizt", b.Angrenzung);
        RaumgrenzenHilfe.Nahe(12.0, a.Flaeche, 1e-9, "4.0 × 3.0");
        RaumgrenzenHilfe.Nahe(12.0, b.Flaeche, 1e-9);
        Assert.Equal(90, a.Azimut);
        Assert.Equal(270, b.Azimut);
        // Süd: R-1 4.0 × 3.0 = 12.0, R-2 3.9 × 3.0 = 11.7 m², beide aus derselben Wand W-1
        RaumgrenzenHilfe.Nahe(12.0, g.Single(x => x.Raum.Kennzeichen == r1 && x.Bauteil?.Kennzeichen == "W-1").Flaeche, 1e-9);
        RaumgrenzenHilfe.Nahe(11.7, g.Single(x => x.Raum.Kennzeichen == r2 && x.Bauteil?.Kennzeichen == "W-1").Flaeche, 1e-9);
        Assert.DoesNotContain(g, x => x.Offen);
    }

    [Fact]
    public void ZweiRaeume_UnbeheizterNachbar()
    {
        var p = ZweiRaeume(out var r1, out var r2);
        RaumgrenzenHilfe.Eigenschaften(p, r2, new JsonObject { ["beheizt"] = false });
        var g = RaumgrenzenHilfe.Rechnen(p).Grenzen;
        Assert.Equal("unbeheizt", g.Single(x => x.Raum.Kennzeichen == r1 && x.Bauteil?.Kennzeichen == "W-5").Angrenzung);
        Assert.Equal("beheizt", g.Single(x => x.Raum.Kennzeichen == r2 && x.Bauteil?.Kennzeichen == "W-5").Angrenzung);
        // Hülle des beheizten Volumens: W-5 gehört dazu (aus R-1), W-1 nur zum Teil (der Teil von R-2 ist keine Hülle)
        var liste = RaumgrenzenAbfragen.HuellbauteileAus(p.Modell, p.Katalog, Raumgrenzen.Rechnen(p.Modell, p.Katalog), null);
        var w5 = liste.Single(x => x.Bauteil.Kennzeichen == "W-5");
        Assert.Equal("unbeheizt", w5.Angrenzung);
        RaumgrenzenHilfe.Nahe(12.0, w5.FlaecheInnen, 1e-9);
        var w1 = liste.Single(x => x.Bauteil.Kennzeichen == "W-1");
        RaumgrenzenHilfe.Nahe(12.0, w1.FlaecheInnen, 1e-9);
        Assert.Contains("zum Teil", w1.Hinweis);
    }

    [Fact]
    public void Korridor_ZweiRaeumeHinterEinerWand_TeilungInDerMitteDerQuerwand()
    {
        // Korridor 6.0 × 1.5 m, darüber zwei Räume, getrennt durch eine Querwand (Achse x = 3050, Dicke 100)
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);
        var kw = RaumgrenzenHilfe.Wand(p, "EG", [0, 1550], [6000, 1550], 100);
        RaumgrenzenHilfe.Wand(p, "EG", [3050, 1600], [3050, 4000], 100);
        var c = RaumgrenzenHilfe.Raum(p, "EG", 3000, 700);
        var a = RaumgrenzenHilfe.Raum(p, "EG", 1500, 3000);
        var b = RaumgrenzenHilfe.Raum(p, "EG", 4500, 3000);
        var g = RaumgrenzenHilfe.Rechnen(p, c).Grenzen.Where(x => x.Bauteil?.Kennzeichen == kw).ToList();
        Assert.Equal(2, g.Count);
        // die Suchlinie kreuzt die Querwand: Teilung in ihrer Mitte (x = 3050) → A 3.05 m, B 2.95 m, je × 3.0 m
        RaumgrenzenHilfe.Nahe(3.05 * 3.0, g.Single(x => x.NachbarRaum?.Kennzeichen == a).Flaeche, 1e-6, "A");
        RaumgrenzenHilfe.Nahe(2.95 * 3.0, g.Single(x => x.NachbarRaum?.Kennzeichen == b).Flaeche, 1e-6, "B");
        Assert.All(g, x => Assert.Equal(0, x.Azimut));
    }

    // ------------------------------------------------------------------ 3) Orientierung mit nordwinkel

    [Fact]
    public void Orientierung_Nordwinkel30()
    {
        var p = Rechteckprojekt(out var raum, false);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI",
            ["eigenschaften"] = new JsonObject { ["nordwinkel"] = 30 },
        }));
        var erg = RaumgrenzenHilfe.Rechnen(p, raum);
        Assert.Equal(30, erg.Nordwinkel);
        double? Az(string kz) => erg.Grenzen.Single(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == kz).Azimut;
        // Projekt-Nord ist um 30° im Uhrzeigersinn gegen geografisch Nord gedreht: Azimut = Winkel ab +y − 30°
        Assert.Equal(150, Az("W-1"));   // Normale −y
        Assert.Equal(60, Az("W-2"));    // +x
        Assert.Equal(330, Az("W-3"));   // +y
        Assert.Equal(240, Az("W-4"));   // −x
        Assert.Equal(150, erg.Grenzen.Single(x => x.Art == "fenster").Azimut);
        Assert.Null(erg.Grenzen.Single(x => x.Art == "boden").Azimut);
        Assert.Equal(0, Raumgrenzen.Azimut(0, 1, 0));
        Assert.Equal(45, Raumgrenzen.Azimut(1, 1, 0));
        Assert.Equal(350, Raumgrenzen.Azimut(0, 1, 10));
    }

    // ------------------------------------------------------------------ 4) Terrain und Untergeschoss

    /// <summary>UG Kote −2800, Höhe 2800, Raum lichte Höhe 2.5 m (z −2800 … −300); EG Kote 0, Raum 2.6 m hoch. Grundriss je
    /// 6.0 × 4.0 m innen wie oben, Fenster im UG Süd 1.0 × 1.0 m, Brüstung 1.3 m (z −1500 … −500).</summary>
    static Projekt MitUntergeschoss(double terrain, out string ug, out string eg)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Eigenschaften(p, "GEB", new JsonObject { ["terrain"] = terrain });
        RaumgrenzenHilfe.Geschoss(p, "UG", -2800, 2800);
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "UG", -100, -100, 6100, 4100, 200);   // W-1 … W-4
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);   // W-5 … W-8
        ug = RaumgrenzenHilfe.Raum(p, "UG", 3000, 2000, 2500);
        eg = RaumgrenzenHilfe.Raum(p, "EG", 3000, 2000, 2600);
        RaumgrenzenHilfe.Fenster(p, "W-1", 3100, 1000, 1000, 1300);
        return p;
    }

    [Fact]
    public void Terrain_1200UeberNull_UGGanzImErdreich_EGNachHoeheGetrennt()
    {
        var p = MitUntergeschoss(1200, out var ug, out var eg);
        var g = RaumgrenzenHilfe.Rechnen(p).Grenzen;
        var u = g.Where(x => x.Raum.Kennzeichen == ug).ToList();
        // UG: Seiten 2 × (6 + 4) × 2.5 = 50 m² inkl. Fenster ganz unter Terrain → Erdreich; Boden unterstes Geschoss → Erdreich
        RaumgrenzenHilfe.Nahe(50.0 + 24.0, RaumgrenzenHilfe.Summe(u, x => x.Angrenzung == "erdreich"), 1e-9, "UG Erdreich");
        Assert.DoesNotContain(u, x => x.Angrenzung == "aussen");
        RaumgrenzenHilfe.Nahe(1.0, u.Single(x => x.Art == "fenster").Flaeche, 1e-9);
        Assert.Equal(eg, u.Single(x => x.Art == "decke").NachbarRaum!.Kennzeichen);
        // EG (z 0 … 2600): unter Terrain 1.2 m Erdreich, darüber 1.4 m aussen
        var e = g.Where(x => x.Raum.Kennzeichen == eg).ToList();
        RaumgrenzenHilfe.Nahe(20 * 1.2, RaumgrenzenHilfe.Summe(e, x => x.Art == "wand" && x.Angrenzung == "erdreich"), 1e-9, "EG Erdreich");
        RaumgrenzenHilfe.Nahe(20 * 1.4, RaumgrenzenHilfe.Summe(e, x => x.Art == "wand" && x.Angrenzung == "aussen"), 1e-9, "EG aussen");
        var sued = e.Where(x => x.Bauteil?.Kennzeichen == "W-5").ToList();
        Assert.Equal(2, sued.Count);
        var unter = sued.Single(x => x.Angrenzung == "erdreich");
        Assert.Equal(0, unter.Unten);
        Assert.Equal(1200, unter.Oben);
        RaumgrenzenHilfe.Nahe(7.2, unter.Flaeche, 1e-9);
        Assert.Equal(ug, e.Single(x => x.Art == "boden").NachbarRaum!.Kennzeichen);
        Assert.Equal("aussen", e.Single(x => x.Art == "dach").Angrenzung);
    }

    [Fact]
    public void Terrain_1200UnterNull_UGNachHoeheGetrennt_FensterAuchGeteilt()
    {
        var p = MitUntergeschoss(-1200, out var ug, out var eg);
        var u = RaumgrenzenHilfe.Rechnen(p, ug).Grenzen;
        // UG z −2800 … −300: unter Terrain 1.6 m, darüber 0.9 m; Fenster z −1500 … −500 → 0.3 m² Erdreich, 0.7 m² aussen
        var fenster = u.Where(x => x.Art == "fenster").ToList();
        RaumgrenzenHilfe.Nahe(0.3, fenster.Single(x => x.Angrenzung == "erdreich").Flaeche, 1e-9);
        RaumgrenzenHilfe.Nahe(0.7, fenster.Single(x => x.Angrenzung == "aussen").Flaeche, 1e-9);
        var sued = u.Where(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == "W-1").ToList();
        RaumgrenzenHilfe.Nahe(6 * 1.6 - 0.3, sued.Single(x => x.Angrenzung == "erdreich").Flaeche, 1e-9);
        RaumgrenzenHilfe.Nahe(6 * 0.9 - 0.7, sued.Single(x => x.Angrenzung == "aussen").Flaeche, 1e-9);
        RaumgrenzenHilfe.Nahe(20 * 1.6, RaumgrenzenHilfe.Summe(u, x => x.Art is "wand" or "fenster" && x.Angrenzung == "erdreich"), 1e-9);
        RaumgrenzenHilfe.Nahe(20 * 0.9, RaumgrenzenHilfe.Summe(u, x => x.Art is "wand" or "fenster" && x.Angrenzung == "aussen"), 1e-9);
        Assert.Equal("erdreich", u.Single(x => x.Art == "boden").Angrenzung);
        // EG über Terrain: alle Seiten aussen
        var e = RaumgrenzenHilfe.Rechnen(p, eg).Grenzen;
        Assert.All(e.Where(x => x.Art == "wand"), x => Assert.Equal("aussen", x.Angrenzung));
    }

    [Fact]
    public void Boden_RestUeberAussenImOberenGeschoss_RestUnterTerrainErdreich()
    {
        // OG-Raum 6 × 6 m kragt 2 m über den EG-Raum 6 × 4 m aus. Wandband-Regel (Entscheid Hauptlauf 18.10.2026): die EG-Nordwand
        // (Achse y = 4100, Dicke 200, Band y 4000 … 4200) liegt unter dem Rest – ihre Innenhälfte y 4000 … 4100 (6.0 × 0.1 = 0.6 m²)
        // geht zum EG-Raum, die Aussenhälfte bleibt aussen. Gegen den Raum 6.0 × 4.1 = 24.6 m² (vorher 24.0), aussen 6.0 × 1.9 = 11.4 m²
        // (vorher 12.0); Summe 36 m² unverändert.
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Geschoss(p, "OG", 3000, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);
        var eg = RaumgrenzenHilfe.Raum(p, "EG", 3000, 2000, 2600);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-OG", ["geschoss"] = "OG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts([0, 0], [6000, 0], [6000, 6000], [0, 6000]) },
        }));
        var b = RaumgrenzenHilfe.Rechnen(p, "R-OG").Grenzen.Where(x => x.Art == "boden").ToList();
        RaumgrenzenHilfe.Nahe(24.6, b.Single(x => x.NachbarRaum?.Kennzeichen == eg).Flaeche, 1e-9);
        RaumgrenzenHilfe.Nahe(11.4, b.Single(x => x.Angrenzung == "aussen").Flaeche, 1e-9);
        // Boden auf oder unter Terrain (Toleranz 20 mm, Entscheid Hauptlauf 18.10.2026): Erdreich, auch im OG; darüber aussen
        foreach (var (terrain, erwartet) in new[] { (3500.0, "erdreich"), (3000.0, "erdreich"), (2985.0, "erdreich"), (2970.0, "aussen") })
        {
            RaumgrenzenHilfe.Eigenschaften(p, "GEB", new JsonObject { ["terrain"] = terrain });
            b = RaumgrenzenHilfe.Rechnen(p, "R-OG").Grenzen.Where(x => x.Art == "boden").ToList();
            Assert.True(erwartet == b.Single(x => x.NachbarRaum is null).Angrenzung, $"Terrain {terrain}");
        }
    }

    [Fact]
    public void Platte_RaeumeVonKoteZuKote_PlatteImRaumDarunter_DickereGewinnt()
    {
        // Räume aus Wänden reichen von Kote zu Kote (Höhe = Geschosshöhe); die Decke liegt dann im oberen Teil des Raums
        // darunter (BKW). Decke D-1 2700 … 2940 (240 dick), Unterlagsboden D-2 2940 … 3000 (60 dick): D-1 ist das Bauteil.
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Geschoss(p, "OG", 3000, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", -100, -100, 6100, 4100, 200);
        var eg = RaumgrenzenHilfe.Raum(p, "EG", 3000, 2000);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-OG", ["geschoss"] = "OG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]) },
        }));
        RaumgrenzenHilfe.Platte(p, "Decke", "D-1", "EG", -200, -200, 6200, 4200, 2940, 240);
        RaumgrenzenHilfe.Platte(p, "Decke", "D-2", "OG", 0, 0, 6000, 4000, 3000, 60);
        var decke = RaumgrenzenHilfe.Rechnen(p, eg).Grenzen.Single(x => x.Art == "decke");
        Assert.Equal("D-1", decke.Bauteil?.Kennzeichen);
        Assert.Equal("R-OG", decke.NachbarRaum?.Kennzeichen);
        RaumgrenzenHilfe.Nahe(24.0, decke.Flaeche, 1e-9);
        Assert.Equal("D-1", RaumgrenzenHilfe.Rechnen(p, "R-OG").Grenzen.Single(x => x.Art == "boden").Bauteil?.Kennzeichen);
    }

    [Fact]
    public void Raumpolygon_BeruehrtSichSelbst_BeideTeilflaechenGerechnet()
    {
        // zwei Quadrate 2 × 2 m, die sich in (2000, 2000) berühren: 8 m², Umfang 16 m, Höhe 3 m (Geschosshöhe)
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-8", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject
            {
                ["art"] = "polygon",
                ["punkte"] = RaumgrenzenHilfe.Pts([0, 0], [2000, 0], [2000, 2000], [4000, 2000], [4000, 4000], [2000, 4000], [2000, 2000], [0, 2000]),
            },
        }));
        var erg = RaumgrenzenHilfe.Rechnen(p, "R-8");
        RaumgrenzenHilfe.Nahe(8.0, erg.Raeume.Single().FlaecheM2, 1e-9);
        RaumgrenzenHilfe.Nahe(8.0, RaumgrenzenHilfe.Summe(erg.Grenzen, x => x.Art == "boden"), 1e-9);
        RaumgrenzenHilfe.Nahe(16 * 3.0, RaumgrenzenHilfe.Summe(erg.Grenzen, x => x.Art == "wand"), 1e-9);
        // Achterschleife (Kanten kreuzen sich in (1000, 1000)): zwei Dreiecke je 1 m², Umfang 2 × (2 × 1.414 + 2.0) m;
        // die Gausssche Formel des Rohpolygons gäbe 0 m²
        RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Raum", ["kennzeichen"] = "R-X", ["geschoss"] = "EG",
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts([10000, 0], [12000, 2000], [12000, 0], [10000, 2000]) },
        }));
        var x = RaumgrenzenHilfe.Rechnen(p, "R-X");
        RaumgrenzenHilfe.Nahe(2.0, x.Raeume.Single().FlaecheM2, 1e-6);
        RaumgrenzenHilfe.Nahe(2.0, RaumgrenzenHilfe.Summe(x.Grenzen, g => g.Art == "boden"), 1e-6);
        RaumgrenzenHilfe.Nahe(2 * (2 * Math.Sqrt(2) + 2.0) * 3.0, RaumgrenzenHilfe.Summe(x.Grenzen, g => g.Art == "wand"), 1e-3);
        Assert.Contains(x.Hinweise, h => h.Contains("in Teilflächen gerechnet"));
    }

    // ------------------------------------------------------------------ 5) Vorgabe angrenzung

    [Fact]
    public void Vorgabe_AngrenzungUeberschreibtMitHinweis()
    {
        var p = Rechteckprojekt(out var raum, false);
        var f = RaumgrenzenHilfe.Fenster(p, "W-3", 3100, 1000, 1000, 900);
        RaumgrenzenHilfe.Eigenschaften(p, "W-3", new JsonObject { ["angrenzung"] = "unbeheizt" });
        var erg = RaumgrenzenHilfe.Rechnen(p, raum);
        var nord = erg.Grenzen.Single(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == "W-3");
        Assert.Equal("unbeheizt", nord.Angrenzung);
        Assert.Equal("aussen", nord.Gerechnet);
        RaumgrenzenHilfe.Nahe(15.6 - 1.0, nord.Flaeche, 1e-9);
        // das Fenster ohne eigene Vorgabe übernimmt die seiner Wand
        var fn = erg.Grenzen.Single(x => x.Bauteil?.Kennzeichen == f);
        Assert.Equal("unbeheizt", fn.Angrenzung);
        Assert.Contains(erg.Hinweise, h => h.Contains("Vorgabe") && h.Contains("W-3"));
        // Vorgabe gleich der Rechnung: keine Abweichung, kein Hinweis
        Assert.Null(erg.Grenzen.Single(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == "W-1").Gerechnet);
    }

    [Fact]
    public void Vorgabe_InnenwandAlsAussenGesetzt_NachbarWirdAussen()
    {
        var p = ZweiRaeume(out var r1, out _);
        RaumgrenzenHilfe.Eigenschaften(p, "W-5", new JsonObject { ["angrenzung"] = "aussen" });
        var a = RaumgrenzenHilfe.Rechnen(p, r1).Grenzen.Single(x => x.Bauteil?.Kennzeichen == "W-5");
        Assert.Equal("aussen", a.Angrenzung);
        Assert.Equal("aussen", a.Nachbar);
        Assert.Null(a.NachbarRaum);
        Assert.Equal("beheizt", a.Gerechnet);
    }

    // ------------------------------------------------------------------ Beispielprojekt

    [Fact]
    public void Beispielprojekt_AlleRaeumeOhneFehler_FlaechenSchliessenAuf()
    {
        var p = Beispielprojekt.Erzeugen(RaumgrenzenHilfe.Kat, null);
        var erg = Raumgrenzen.Rechnen(p.Modell, p.Katalog);
        Assert.Equal(p.Modell.DerKlasse("Raum").Count(), erg.Raeume.Count);
        foreach (var rm in erg.Raeume)
        {
            var g = erg.Grenzen.Where(x => x.Raum.Id == rm.Raum.Id).ToList();
            // Boden und Decke je gleich der Raumfläche; Seiten = Umfang × Höhe
            RaumgrenzenHilfe.Nahe(rm.FlaecheM2, RaumgrenzenHilfe.Summe(g, x => x.Art == "boden"), 1e-6, rm.Raum.Kennzeichen + " Boden");
            RaumgrenzenHilfe.Nahe(rm.FlaecheM2, RaumgrenzenHilfe.Summe(g, x => x.Art is "decke" or "dach"), 1e-6, rm.Raum.Kennzeichen + " Decke");
            var ring = rm.Raum.Geometrie!.Punkte;
            double umfang = ring.Select((q, i) => Geometrie.Abstand(new[] { q[0], q[1] }, new[] { ring[(i + 1) % ring.Count][0], ring[(i + 1) % ring.Count][1] })).Sum();
            RaumgrenzenHilfe.Nahe(umfang / 1000 * rm.HoeheMm / 1000, RaumgrenzenHilfe.Summe(g, x => x.Art is "wand" or "fenster" or "tuer"), 1e-6, rm.Raum.Kennzeichen + " Seiten");
        }
        // Die Aussenwände des Beispiels tragen angrenzung als Vorgabe (UG erdreich, sonst aussen)
        Assert.All(erg.Grenzen.Where(x => x.Bauteil is not null && x.Art == "wand" && x.Bauteil.Kennzeichen.StartsWith("W-UG")), x => Assert.Equal("erdreich", x.Angrenzung));
        var o = (JsonObject)Tuer.Abfrage(p, "raumgrenzen");
        Assert.Equal(erg.Grenzen.Count, o["zeilen"]!.AsArray().Count);
        Assert.NotNull(Tuer.Abfrage(p, "huellbauteile"));
    }
}
