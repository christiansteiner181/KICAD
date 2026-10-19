using System.Text.Json.Nodes;

namespace Hlks.Kern.Tests;

/// <summary>Modul 1c, Auftrag B5: unabhängige Referenzfälle von Hand. Jeder Erwartungswert ist aus Geometrie und Formel
/// gerechnet (Rechenweg im Kommentar über der Prüfung), nie durch Aufruf einer Produktfunktion bestimmt. Alle Kennwerte
/// (λ, Rsi, Rse, Ug, Uf, ψg, Rahmenanteil, Ud) sind Testwerte ohne Bezug zu einer Norm.
///
/// Gebäude «Haus»: Aussenmass 10.00 × 8.00 m, Aussenwände 300 mm → Achsen (150, 150) … (9850, 7850), Innenseiten
/// x 300 … 9700 (9.40 m), y 300 … 7700 (7.40 m). Innenwand 150 mm, Achse x = 4000 → Flächen x 3925 … 4075 (nur EG).
/// EG Kote 0, Höhe 3000: R-A (West) x 300 … 3925 = 3.625 m breit, R-B (Ost) x 4075 … 9700 = 5.625 m breit, je 7.40 m tief.
/// R-A = 3.625 × 7.40 = 26.825 m², R-B = 5.625 × 7.40 = 41.625 m². OG Kote 3000, Höhe 3000: ein Raum R-OG über der ganzen
/// Innenfläche 9.40 × 7.40 = 69.56 m². Lichte Raumhöhe überall 2.60 m. Terrain ±0 (EG liegt auf ±0).
/// Decke über EG: OK 3000, 250 dick; Boden EG: OK 0, 300 dick; Dach: OK 6000, 250 dick (alle mit decke_erzeugen).</summary>
public class Referenz1cTests
{
    const string Q = "Testwert B5 (kein Materialkennwert)";
    const double H = 2.6;   // lichte Höhe in m

    sealed class Haus
    {
        public required Projekt P;
        public required string RA, RB, ROG;
        public required List<string> OgWaende;
        public required string Sued, Ost, Nord, West, Innen;     // EG-Wände
        public required string DeckeEg, BodenEg, Dach;           // Platten
        public string? Fenster, Tuer;
    }

    static string Elementkz(BefehlsErgebnis e, params string[] klassen) =>
        e.Aenderungen.Single(a => klassen.Contains(a.Klasse) && a.Art == "angelegt").Kennzeichen;

    /// <summary>oeffnungenInRA: Fenster 1.50 × 1.20 (Brüstung 0.90) in der Südwand und Tür 1.00 × 2.10 in der Westwand, beide in R-A.</summary>
    static Haus Bauen(bool bBeheizt = false, double? terrain = null, double? nordwinkel = null, bool oeffnungenInRA = false)
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Geschoss(p, "OG", 3000, 3000);
        var eg = RaumgrenzenHilfe.Waende(p, "EG", 150, 150, 9850, 7850, 300);   // Süd, Ost, Nord, West
        var innen = RaumgrenzenHilfe.Wand(p, "EG", [4000, 300], [4000, 7700], 150);
        var og = RaumgrenzenHilfe.Waende(p, "OG", 150, 150, 9850, 7850, 300);
        var ra = RaumgrenzenHilfe.Raum(p, "EG", 2000, 4000, 2600, "0.01");
        var rb = RaumgrenzenHilfe.Raum(p, "EG", 7000, 4000, 2600, "0.02");
        var rog = RaumgrenzenHilfe.Raum(p, "OG", 5000, 4000, 2600, "1.01");
        if (!bBeheizt) RaumgrenzenHilfe.Eigenschaften(p, rb, new JsonObject { ["beheizt"] = false });
        if (terrain is double t) RaumgrenzenHilfe.Eigenschaften(p, "GEB", new JsonObject { ["terrain"] = t });
        if (nordwinkel is double nw)
            RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI", ["eigenschaften"] = new JsonObject { ["nordwinkel"] = nw },
            }));
        var decke = Elementkz(RaumgrenzenHilfe.Ok(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "decke", ["dicke"] = 250 })), "Decke");
        var boden = Elementkz(RaumgrenzenHilfe.Ok(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = "EG", ["art"] = "boden", ["dicke"] = 300 })), "Decke");
        var dach = Elementkz(RaumgrenzenHilfe.Ok(p.Ausfuehren("decke_erzeugen", new JsonObject { ["geschoss"] = "OG", ["art"] = "dach", ["dicke"] = 250 })), "Dach");
        var h = new Haus
        {
            P = p, OgWaende = og, RA = ra, RB = rb, ROG = rog, Sued = eg[0], Ost = eg[1], Nord = eg[2], West = eg[3], Innen = innen,
            DeckeEg = decke, BodenEg = boden, Dach = dach,
        };
        if (oeffnungenInRA)
        {
            // Fenster-Mitte x = 2100 → abstand ab Wandanfang (x = 150) = 1950; Tür-Mitte y = 4000 → abstand ab Wandanfang (y = 150) = 3850
            h.Fenster = RaumgrenzenHilfe.Fenster(p, h.Sued, 1950, 1500, 1200, 900);
            h.Tuer = RaumgrenzenHilfe.Tuer(p, h.West, 3850, 1000, 2100);
        }
        return h;
    }

    static List<Raumgrenze> Von(RaumgrenzenErgebnis e, string raum) => e.Grenzen.Where(g => g.Raum.Kennzeichen == raum).ToList();

    static Raumgrenze Eine(IEnumerable<Raumgrenze> g, string art, Func<Raumgrenze, bool>? wo = null) =>
        g.Where(x => x.Art == art && (wo?.Invoke(x) ?? true)).Single();

    static Raumgrenze Wand(IEnumerable<Raumgrenze> g, string wand) => Eine(g, "wand", x => x.Bauteil?.Kennzeichen == wand);

    static void Nahe(double erwartet, double ist, string text, double tol = 1e-6) => RaumgrenzenHilfe.Nahe(erwartet, ist, tol, text);

    static void Pruefe(Raumgrenze g, string angrenzung, string nachbar, double flaeche, double? azimut, double neigung, string text)
    {
        Assert.True(angrenzung == g.Angrenzung, $"{text}: Angrenzung erwartet {angrenzung}, ist {g.Angrenzung}");
        string ist = g.NachbarRaum?.Kennzeichen ?? g.Nachbar;
        Assert.True(nachbar == ist, $"{text}: Nachbar erwartet {nachbar}, ist {ist}");
        Nahe(flaeche, g.Flaeche, text + " Fläche");
        if (azimut is null) Assert.True(g.Azimut is null, $"{text}: Azimut erwartet leer, ist {g.Azimut}");
        else { Assert.NotNull(g.Azimut); Nahe(azimut.Value, g.Azimut!.Value, text + " Azimut", 1e-9); }
        Nahe(neigung, g.Neigung, text + " Neigung", 1e-9);
    }

    // ------------------------------------------------------------------ Fall 1: kleines Gebäude, zwei Geschosse

    [Fact]
    public void Fall1_RaumA_BeheiztWest_JedeRaumgrenze()
    {
        var h = Bauen();
        var e = RaumgrenzenHilfe.Rechnen(h.P);
        var a = Von(e, h.RA);
        // R-A: Südkante x 300 … 3925 = 3.625 m; × 2.60 m = 9.425 m²; Nordkante gleich; Westkante 7.40 m × 2.60 m = 19.24 m²;
        // Ostkante = Innenwand 7.40 m × 2.60 = 19.24 m² gegen R-B (unbeheizt). Boden = Decke = 3.625 × 7.40 = 26.825 m².
        Pruefe(Wand(a, h.Sued), "aussen", "aussen", 3.625 * H, 180, 90, "R-A Süd");
        Pruefe(Wand(a, h.Nord), "aussen", "aussen", 3.625 * H, 0, 90, "R-A Nord");
        Pruefe(Wand(a, h.West), "aussen", "aussen", 7.4 * H, 270, 90, "R-A West");
        Pruefe(Wand(a, h.Innen), "unbeheizt", h.RB, 7.4 * H, 90, 90, "R-A Innenwand");
        Pruefe(Eine(a, "boden"), "erdreich", "erdreich", 26.825, null, 180, "R-A Boden");
        Assert.Equal(h.BodenEg, Eine(a, "boden").Bauteil?.Kennzeichen);
        Pruefe(Eine(a, "decke"), "beheizt", h.ROG, 26.825, null, 0, "R-A Decke");
        Assert.Equal(h.DeckeEg, Eine(a, "decke").Bauteil?.Kennzeichen);
        Assert.Equal(6, a.Count);
        Assert.DoesNotContain(a, x => x.Offen);
        Nahe(9.425, Wand(a, h.Sued).Flaeche, "Zahlenprobe Süd", 1e-9);
        Nahe(19.24, Wand(a, h.West).Flaeche, "Zahlenprobe West", 1e-9);
    }

    [Fact]
    public void Fall1_RaumB_UnbeheiztOst_JedeRaumgrenze()
    {
        var h = Bauen();
        var b = Von(RaumgrenzenHilfe.Rechnen(h.P), h.RB);
        // R-B: Südkante x 4075 … 9700 = 5.625 m × 2.60 = 14.625 m²; Nordkante gleich; Ostkante 7.40 × 2.60 = 19.24 m²;
        // Westkante = Innenwand 19.24 m² gegen R-A (beheizt). Boden = Decke = 5.625 × 7.40 = 41.625 m².
        Pruefe(Wand(b, h.Sued), "aussen", "aussen", 5.625 * H, 180, 90, "R-B Süd");
        Pruefe(Wand(b, h.Nord), "aussen", "aussen", 5.625 * H, 0, 90, "R-B Nord");
        Pruefe(Wand(b, h.Ost), "aussen", "aussen", 7.4 * H, 90, 90, "R-B Ost");
        Pruefe(Wand(b, h.Innen), "beheizt", h.RA, 7.4 * H, 270, 90, "R-B Innenwand");
        Pruefe(Eine(b, "boden"), "erdreich", "erdreich", 41.625, null, 180, "R-B Boden");
        Pruefe(Eine(b, "decke"), "beheizt", h.ROG, 41.625, null, 0, "R-B Decke");
        Assert.Equal(6, b.Count);
    }

    [Fact]
    public void Fall1_RaumOG_SeitenDachUndBodenGegenDieRaeumeDarunter()
    {
        var h = Bauen();
        var o = Von(RaumgrenzenHilfe.Rechnen(h.P), h.ROG);
        // R-OG: Süd/Nord 9.40 × 2.60 = 24.44 m²; Ost/West 7.40 × 2.60 = 19.24 m²; Dach = 9.40 × 7.40 = 69.56 m² (aussen, Neigung 0).
        Pruefe(Wand(o, h.OgWaende[0]), "aussen", "aussen", 9.4 * H, 180, 90, "OG Süd");
        Pruefe(Wand(o, h.OgWaende[1]), "aussen", "aussen", 7.4 * H, 90, 90, "OG Ost");
        Pruefe(Wand(o, h.OgWaende[2]), "aussen", "aussen", 9.4 * H, 0, 90, "OG Nord");
        Pruefe(Wand(o, h.OgWaende[3]), "aussen", "aussen", 7.4 * H, 270, 90, "OG West");
        Pruefe(Eine(o, "dach"), "aussen", "aussen", 69.56, null, 0, "OG Dach");
        Assert.Equal(h.Dach, Eine(o, "dach").Bauteil?.Kennzeichen);
        // Boden: Streifen über der Innenwand EG x 3925 … 4075 (0.15 m × 7.40 m = 1.11 m²) hat keinen Raum darunter. Wandband-Regel
        // (Entscheid Hauptlauf 18.10.2026): an der Achse x = 4000 geteilt, West 3925 … 4000 = 0.075 × 7.40 = 0.555 m² → R-A,
        // Ost 4000 … 4075 = 0.555 m² → R-B. Gegen R-A also x 300 … 4000 = 3.70 × 7.40 = 27.38 m² (= 26.825 + 0.555), beheizt;
        // gegen R-B x 4000 … 9700 = 5.70 × 7.40 = 42.18 m² (= 41.625 + 0.555), unbeheizt. Summe 27.38 + 42.18 = 69.56 = Raumfläche.
        // Neigung 180, Bauteil = Decke über EG (überdeckt auch den Streifen).
        var boeden = o.Where(x => x.Art == "boden").ToList();
        var gegenA = boeden.Single(x => x.NachbarRaum?.Kennzeichen == h.RA);
        var gegenB = boeden.Single(x => x.NachbarRaum?.Kennzeichen == h.RB);
        Pruefe(gegenA, "beheizt", h.RA, 3.70 * 7.40, null, 180, "OG Boden über R-A samt halbem Innenwandstreifen");
        Pruefe(gegenB, "unbeheizt", h.RB, 5.70 * 7.40, null, 180, "OG Boden über R-B samt halbem Innenwandstreifen");
        Nahe(26.825 + 0.555, gegenA.Flaeche, "Zahlenprobe R-A");
        Nahe(41.625 + 0.555, gegenB.Flaeche, "Zahlenprobe R-B");
        Assert.Equal(h.DeckeEg, gegenA.Bauteil?.Kennzeichen);
        Assert.Equal(h.DeckeEg, gegenB.Bauteil?.Kennzeichen);
        // kein Boden gegen aussen mehr im OG
        Assert.Equal(2, boeden.Count);
        Assert.DoesNotContain(boeden, x => x.NachbarRaum is null);
        Assert.DoesNotContain(boeden, x => x.Angrenzung == "aussen");
    }

    [Fact]
    public void Fall1_Bodenbilanz_SummeBodenGleichSummeRaumflaechen()
    {
        var h = Bauen();
        var e = RaumgrenzenHilfe.Rechnen(h.P);
        // Raumflächen von Hand: R-A 26.825, R-B 41.625, R-OG 69.56 → zusammen 138.01 m²
        double soll = 26.825 + 41.625 + 69.56;
        Nahe(soll, e.Raeume.Sum(r => r.FlaecheM2), "Raumflächen", 1e-9);
        Nahe(soll, RaumgrenzenHilfe.Summe(e.Grenzen, x => x.Art == "boden"), "Summe Boden");
        Nahe(26.825, e.Raeume.Single(r => r.Raum.Kennzeichen == h.RA).FlaecheM2, "R-A", 1e-9);
        Nahe(41.625, e.Raeume.Single(r => r.Raum.Kennzeichen == h.RB).FlaecheM2, "R-B", 1e-9);
        Nahe(69.56, e.Raeume.Single(r => r.Raum.Kennzeichen == h.ROG).FlaecheM2, "R-OG", 1e-9);
        // je Raum: Boden = Raumfläche und Decke bzw. Dach = Raumfläche
        foreach (var r in e.Raeume)
        {
            Nahe(r.FlaecheM2, RaumgrenzenHilfe.Summe(Von(e, r.Raum.Kennzeichen), x => x.Art == "boden"), r.Raum.Kennzeichen + " Boden");
            Nahe(r.FlaecheM2, RaumgrenzenHilfe.Summe(Von(e, r.Raum.Kennzeichen), x => x.Art is "decke" or "dach"), r.Raum.Kennzeichen + " Decke/Dach");
            Assert.Equal(2600, r.HoeheMm);
        }
    }

    [Fact]
    public void Fall1_SummenJeAngrenzung()
    {
        var h = Bauen();
        var e = RaumgrenzenHilfe.Rechnen(h.P);
        double S(string angrenzung) => RaumgrenzenHilfe.Summe(e.Grenzen, x => x.Angrenzung == angrenzung);
        // Wandband-Regel (Entscheid Hauptlauf 18.10.2026): der Boden-Streifen über der Innenwand EG (1.11 m²) ist nicht mehr aussen,
        // sondern je 0.555 m² bei R-A (beheizt) und R-B (unbeheizt) → aussen −1.11, beheizt +0.555, unbeheizt +0.555.
        // aussen: EG Seiten R-A 9.425 + 19.24 + 9.425 = 38.09; R-B 14.625 + 19.24 + 14.625 = 48.49; OG Seiten 2 × 24.44 + 2 × 19.24 = 87.36;
        //         Dach 69.56 → 38.09 + 48.49 + 87.36 + 69.56 = 243.50 m² (vorher 244.61 mit dem Streifen)
        Nahe(38.09 + 48.49 + 87.36 + 69.56, S("aussen"), "aussen");
        Nahe(243.50, S("aussen"), "aussen Zahlenprobe");
        // erdreich: Boden EG 26.825 + 41.625 = 68.45 m² (unverändert)
        Nahe(68.45, S("erdreich"), "erdreich");
        // unbeheizt: Innenwand aus R-A 19.24 + Boden OG über R-B 42.18 (= 41.625 + 0.555) = 61.42 m² (vorher 60.865)
        Nahe(19.24 + 42.18, S("unbeheizt"), "unbeheizt");
        Nahe(60.865 + 0.555, S("unbeheizt"), "unbeheizt Zahlenprobe");
        // beheizt: Innenwand aus R-B 19.24 + Decke R-A 26.825 + Decke R-B 41.625 + Boden OG über R-A 27.38 (= 26.825 + 0.555)
        //          = 115.07 m² (vorher 114.515)
        Nahe(19.24 + 26.825 + 41.625 + 27.38, S("beheizt"), "beheizt");
        Nahe(114.515 + 0.555, S("beheizt"), "beheizt Zahlenprobe");
        // Gegenprobe über den Gesamtbestand der Flächen:
        // Seiten EG 2 × 19.24 (Innenwand beidseitig) + 38.09 + 48.49 = 125.06, OG 87.36, Böden 138.01, Decken/Dach 138.01 → 488.44 m²
        Nahe(125.06 + 87.36 + 138.01 + 138.01, e.Grenzen.Sum(x => x.Flaeche), "alle Grenzen");
    }

    // ------------------------------------------------------------------ Fall 2: Nordwinkel 30°

    [Fact]
    public void Fall2_Nordwinkel30_Azimute()
    {
        var h = Bauen(nordwinkel: 30);
        var e = RaumgrenzenHilfe.Rechnen(h.P);
        Assert.Equal(30, e.Nordwinkel);
        // Azimut = Winkel der Aussennormalen ab Projekt-Nord (+y, im Uhrzeigersinn) − nordwinkel
        // Nordwand: Normale +y → 0° − 30° = −30° → 330°;  Ostwand: 90° − 30° = 60°;  Südwand: 180° − 30° = 150°;  Westwand: 270° − 30° = 240°
        // Innenwand (Normale aus R-A nach +x) = 90° − 30° = 60°, aus R-B (nach −x) = 270° − 30° = 240°
        var b = Von(e, h.RB);
        var a = Von(e, h.RA);
        Assert.Equal(330, Wand(b, h.Nord).Azimut);
        Assert.Equal(60, Wand(b, h.Ost).Azimut);
        Assert.Equal(150, Wand(b, h.Sued).Azimut);
        Assert.Equal(240, Wand(a, h.West).Azimut);
        Assert.Equal(60, Wand(a, h.Innen).Azimut);
        Assert.Equal(240, Wand(b, h.Innen).Azimut);
        Assert.Null(Eine(a, "boden").Azimut);
        Assert.Null(Eine(a, "decke").Azimut);
        Assert.Equal(180, Eine(a, "boden").Neigung);
        // Ohne nordwinkel (Gegenprobe der Vorgabe 0): Nord 0°, Ost 90°
        var ohne = RaumgrenzenHilfe.Rechnen(Bauen().P);
        Assert.Equal(0, Wand(Von(ohne, h.RB), h.Nord).Azimut);
        Assert.Equal(90, Wand(Von(ohne, h.RB), h.Ost).Azimut);
    }

    // ------------------------------------------------------------------ Fall 3: Fenster und Tür

    static void Kennwerte(Haus h)
    {
        // Testwerte: Ug 1.0, Uf 2.0, ψg 0.1, Rahmenanteil 0.36 (√(1 − 0.36) = 0.8), Ud 1.5
        RaumgrenzenHilfe.Ok(h.P.Ausfuehren("fenster_kennwerte", new JsonObject
        {
            ["elemente"] = new JsonArray(h.Fenster!), ["ug"] = 1.0, ["uf"] = 2.0, ["psiG"] = 0.1, ["rahmenanteil"] = 0.36, ["quelle"] = Q,
        }));
        RaumgrenzenHilfe.Ok(h.P.Ausfuehren("fenster_kennwerte", new JsonObject
        {
            ["elemente"] = new JsonArray(h.Tuer!), ["ud"] = 1.5, ["quelle"] = Q,
        }));
    }

    [Fact]
    public void Fall3_OeffnungenWerdenAbgezogenUndEigeneRaumgrenze_UwUndUA()
    {
        var h = Bauen(oeffnungenInRA: true);
        Kennwerte(h);
        var a = Von(RaumgrenzenHilfe.Rechnen(h.P), h.RA);
        // Fenster 1.50 × 1.20 = 1.80 m²; Südwand R-A: 3.625 × 2.60 − 1.80 = 9.425 − 1.80 = 7.625 m²
        // Tür 1.00 × 2.10 = 2.10 m²; Westwand R-A: 7.40 × 2.60 − 2.10 = 19.24 − 2.10 = 17.14 m²
        Pruefe(Wand(a, h.Sued), "aussen", "aussen", 7.625, 180, 90, "Südwand ohne Fenster");
        Pruefe(Wand(a, h.West), "aussen", "aussen", 17.14, 270, 90, "Westwand ohne Tür");
        var f = Eine(a, "fenster");
        var t = Eine(a, "tuer");
        Assert.Equal(h.Fenster, f.Bauteil?.Kennzeichen);
        Assert.Equal(h.Tuer, t.Bauteil?.Kennzeichen);
        Pruefe(f, "aussen", "aussen", 1.8, 180, 90, "Fenster");
        Pruefe(t, "aussen", "aussen", 2.1, 270, 90, "Tür");
        // Summe der Aussenseiten bleibt: 7.625 + 1.80 + 9.425 + 17.14 + 2.10 = 38.09 m²
        Nahe(38.09, RaumgrenzenHilfe.Summe(a, x => x.Art is "wand" or "fenster" or "tuer" && x.Angrenzung == "aussen"), "Aussenseiten R-A");
        // Uw von Hand: A = 1.8 m², Af = 0.36 · 1.8 = 0.648 m², Ag = 1.152 m², lg = 2·(1.5 + 1.2)·√0.64 = 5.4 · 0.8 = 4.32 m
        // Uw = (1.152·1.0 + 0.648·2.0 + 4.32·0.1)/1.8 = (1.152 + 1.296 + 0.432)/1.8 = 2.880/1.8 = 1.6 W/(m²·K)
        Assert.NotNull(f.U);
        Nahe(1.6, f.U!.Value, "Uw Fenster", 1e-9);
        // U·A Fenster = 1.6 · 1.8 = 2.88 W/K; Tür: 1.5 · 2.1 = 3.15 W/K
        Nahe(2.88, f.UA!.Value, "UA Fenster", 1e-9);
        Assert.NotNull(t.U);
        Nahe(1.5, t.U!.Value, "Ud Tür", 1e-9);
        Nahe(3.15, t.UA!.Value, "UA Tür", 1e-9);
        // Wände ohne Aufbau: kein U, kein UA
        Assert.Null(Wand(a, h.Sued).U);
        Assert.Null(Wand(a, h.Sued).UA);
    }

    // ------------------------------------------------------------------ Fall 4: teilweise unter Terrain

    [Fact]
    public void Fall4_Terrain1200_WandNachHoeheGeteilt()
    {
        var h = Bauen(terrain: 1200);
        var e = RaumgrenzenHilfe.Rechnen(h.P);
        var a = Von(e, h.RA);
        // EG-Wand z 0 … 2600, Terrain +1.20 m: unten 1.20 m erdreich, darüber 1.40 m aussen.
        // R-A Süd: 3.625 · 1.2 = 4.35 erdreich, 3.625 · 1.4 = 5.075 aussen; Nord gleich; West: 7.4 · 1.2 = 8.88 / 7.4 · 1.4 = 10.36
        void Teilung(IEnumerable<Raumgrenze> g, string wand, double laenge, string text, double? az)
        {
            var teile = g.Where(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == wand).ToList();
            Assert.Equal(2, teile.Count);
            var unten = teile.Single(x => x.Angrenzung == "erdreich");
            var oben = teile.Single(x => x.Angrenzung == "aussen");
            Nahe(laenge * 1.2, unten.Flaeche, text + " erdreich");
            Nahe(laenge * 1.4, oben.Flaeche, text + " aussen");
            Nahe(0, unten.Unten, text + " Unterkante erdreich", 1e-9);
            Nahe(1200, unten.Oben, text + " Oberkante erdreich", 1e-9);
            Nahe(1200, oben.Unten, text + " Unterkante aussen", 1e-9);
            Nahe(2600, oben.Oben, text + " Oberkante aussen", 1e-9);
            Assert.Equal(az, unten.Azimut);
            Assert.Equal(az, oben.Azimut);
            Assert.Equal("erdreich", unten.Nachbar);
        }
        Teilung(a, h.Sued, 3.625, "R-A Süd", 180);
        Teilung(a, h.Nord, 3.625, "R-A Nord", 0);
        Teilung(a, h.West, 7.4, "R-A West", 270);
        // Innenwand bleibt ungeteilt (Nachbar Raum), Boden erdreich
        Pruefe(Wand(a, h.Innen), "unbeheizt", h.RB, 7.4 * H, 90, 90, "Innenwand");
        // Summen R-A Wände: erdreich 4.35 + 4.35 + 8.88 = 17.58; aussen 5.075 + 5.075 + 10.36 = 20.51
        Nahe(17.58, RaumgrenzenHilfe.Summe(a, x => x.Art == "wand" && x.Angrenzung == "erdreich"), "R-A Wände erdreich");
        Nahe(20.51, RaumgrenzenHilfe.Summe(a, x => x.Art == "wand" && x.Angrenzung == "aussen"), "R-A Wände aussen");
        // R-B: Süd/Nord je 5.625 · 1.2 = 6.75 / 5.625 · 1.4 = 7.875; Ost 8.88 / 10.36 → erdreich 22.38, aussen 26.11
        var b = Von(e, h.RB);
        Nahe(22.38, RaumgrenzenHilfe.Summe(b, x => x.Art == "wand" && x.Angrenzung == "erdreich"), "R-B Wände erdreich");
        Nahe(26.11, RaumgrenzenHilfe.Summe(b, x => x.Art == "wand" && x.Angrenzung == "aussen"), "R-B Wände aussen");
        // OG liegt über Terrain: nichts erdreich
        Assert.DoesNotContain(Von(e, h.ROG), x => x.Angrenzung == "erdreich");
        Nahe(87.36, RaumgrenzenHilfe.Summe(Von(e, h.ROG), x => x.Art == "wand" && x.Angrenzung == "aussen"), "OG Seiten aussen");
    }

    [Fact]
    public void Fall4_Terrain1200_OeffnungenAuchGeteilt()
    {
        var h = Bauen(terrain: 1200, oeffnungenInRA: true);
        var a = Von(RaumgrenzenHilfe.Rechnen(h.P), h.RA);
        // Fenster z 900 … 2100 (1.50 breit): 900 … 1200 → 0.30 · 1.5 = 0.45 m² erdreich; 1200 … 2100 → 0.90 · 1.5 = 1.35 m² aussen
        var fenster = a.Where(x => x.Art == "fenster").ToList();
        Nahe(0.45, fenster.Single(x => x.Angrenzung == "erdreich").Flaeche, "Fenster erdreich");
        Nahe(1.35, fenster.Single(x => x.Angrenzung == "aussen").Flaeche, "Fenster aussen");
        // Tür z 0 … 2100 (1.00 breit): 1.20 m² erdreich, 0.90 m² aussen
        var tueren = a.Where(x => x.Art == "tuer").ToList();
        Nahe(1.2, tueren.Single(x => x.Angrenzung == "erdreich").Flaeche, "Tür erdreich");
        Nahe(0.9, tueren.Single(x => x.Angrenzung == "aussen").Flaeche, "Tür aussen");
        // Südwand: erdreich 4.35 − 0.45 = 3.90; aussen 5.075 − 1.35 = 3.725
        var sued = a.Where(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == h.Sued).ToList();
        Nahe(3.90, sued.Single(x => x.Angrenzung == "erdreich").Flaeche, "Süd erdreich");
        Nahe(3.725, sued.Single(x => x.Angrenzung == "aussen").Flaeche, "Süd aussen");
        // Westwand: erdreich 8.88 − 1.20 = 7.68; aussen 10.36 − 0.90 = 9.46
        var west = a.Where(x => x.Art == "wand" && x.Bauteil?.Kennzeichen == h.West).ToList();
        Nahe(7.68, west.Single(x => x.Angrenzung == "erdreich").Flaeche, "West erdreich");
        Nahe(9.46, west.Single(x => x.Angrenzung == "aussen").Flaeche, "West aussen");
        // Summe über alles aus R-A gegen aussen/erdreich unverändert: 17.58 + 20.51 = 38.09 m²
        Nahe(38.09, RaumgrenzenHilfe.Summe(a, x => x.Art is "wand" or "fenster" or "tuer" && x.Angrenzung is "aussen" or "erdreich"), "Seiten R-A");
    }

    // ------------------------------------------------------------------ Fall 5: Aufbau, R, U

    static string MaterialMitQuelle(Projekt p, string name, double lambda) =>
        RaumgrenzenHilfe.Ok(p.Ausfuehren("material_anlegen", new JsonObject
        {
            ["name"] = name, ["werte"] = new JsonObject { ["lambda"] = new JsonObject { ["wert"] = lambda, ["quelle"] = Q } },
        })).Aenderungen.Single(a => a.Klasse == "Material").Kennzeichen;

    static void UebergangHorizontal(Projekt p, double rsi, double rse) =>
        RaumgrenzenHilfe.Ok(p.Ausfuehren("uebergang_setzen", new JsonObject
        {
            ["horizontal"] = new JsonObject { ["rsi"] = rsi, ["rse"] = rse }, ["quelle"] = Q,
        }));

    [Fact]
    public void Fall5_DreiSchichten_RUndU()
    {
        var p = ArchitekturHilfe.Neu();
        var a = MaterialMitQuelle(p, "Testschicht A", 0.5);
        var b = MaterialMitQuelle(p, "Testschicht B", 0.04);
        var c = MaterialMitQuelle(p, "Testschicht C", 0.8);
        UebergangHorizontal(p, 0.13, 0.04);
        var e = RaumgrenzenHilfe.Ok(HuelleTestHilfe.Aufbau(p, "Testwand", "wand", HuelleTestHilfe.Schichten((a, 100), (b, 120), (c, 80))));
        var auf = Elementkz(e, "Aufbau");
        // R1 = 0.100/0.5 = 0.2;  R2 = 0.120/0.04 = 3.0;  R3 = 0.080/0.8 = 0.1;  R = 3.3 m²K/W
        // U = 1/(0.13 + 3.3 + 0.04) = 1/3.47 = 0.2881844… W/(m²·K)
        HuelleTestHilfe.Gleich(3.3, HuelleTestHilfe.Zahl(p, auf, "rWert"), 1e-9);
        HuelleTestHilfe.Gleich(1.0 / 3.47, HuelleTestHilfe.Zahl(p, auf, "uWert"), 1e-9);
        var q = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = auf });
        HuelleTestHilfe.Gleich(0.2, q["schichten"]![0]!["r"]!.GetValue<double>(), 1e-6);
        HuelleTestHilfe.Gleich(3.0, q["schichten"]![1]!["r"]!.GetValue<double>(), 1e-6);
        HuelleTestHilfe.Gleich(0.1, q["schichten"]![2]!["r"]!.GetValue<double>(), 1e-6);
        Assert.Equal(0.13, q["rsi"]!.GetValue<double>());
        Assert.Equal(0.04, q["rse"]!.GetValue<double>());
        Assert.Empty(q["fehlt"]!.AsArray());
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), x => x.Code == "P12");
    }

    [Fact]
    public void Fall5_SchichtOhneLambdaQuelle_KeinU_NurRDerBekanntenSchichten_P12()
    {
        var p = ArchitekturHilfe.Neu();
        var a = MaterialMitQuelle(p, "Testschicht A", 0.5);
        var c = MaterialMitQuelle(p, "Testschicht C", 0.8);
        UebergangHorizontal(p, 0.13, 0.04);
        // Schicht B: λ = 0.04 am Befehl vorbei (element_anlegen) ohne Quelle
        var ohne = Elementkz(RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Material", ["name"] = "Testschicht B ohne Quelle", ["eigenschaften"] = new JsonObject { ["lambda"] = 0.04 },
        })), "Material");
        var e = RaumgrenzenHilfe.Ok(HuelleTestHilfe.Aufbau(p, "Testwand ohne Quelle", "wand", HuelleTestHilfe.Schichten((a, 100), (ohne, 120), (c, 80))));
        var auf = Elementkz(e, "Aufbau");
        // bekannte Schichten: 0.100/0.5 = 0.2 und 0.080/0.8 = 0.1 → Teilsumme 0.3; die mittlere zählt nicht → kein R gesamt, kein U
        Assert.Null(ArchitekturHilfe.Hole(p, auf).Eigenschaft("rWert"));
        Assert.Null(ArchitekturHilfe.Hole(p, auf).Eigenschaft("uWert"));
        Assert.Contains(e.Hinweise, x => x.Contains("kein U-Wert") && x.Contains(ohne));
        var q = (JsonObject)Tuer.Abfrage(p, "aufbau_rechnen", new JsonObject { ["aufbau"] = auf });
        Assert.Null(q["u"]);
        Assert.Null(q["r"]);
        HuelleTestHilfe.Gleich(0.2, q["schichten"]![0]!["r"]!.GetValue<double>(), 1e-6);
        Assert.Null(q["schichten"]![1]!["r"]);
        HuelleTestHilfe.Gleich(0.1, q["schichten"]![2]!["r"]!.GetValue<double>(), 1e-6);
        double teilsumme = q["schichten"]!.AsArray().Sum(s => s!["r"]?.GetValue<double>() ?? 0);
        HuelleTestHilfe.Gleich(0.3, teilsumme, 1e-6);
        Assert.Contains("keine Quelle", q["fehlt"]!.ToJsonString());
        // P12 meldet genau diesen Kennwert
        var p12 = Pruefungen.Alle(p.Modell, p.Katalog).Where(x => x.Code == "P12").ToList();
        var b = Assert.Single(p12);
        Assert.Equal(ohne, b.Element);
        Assert.Contains("lambda", b.Text);
    }

    // ------------------------------------------------------------------ Fall 6: U·A einer Wand mit Aufbau

    [Fact]
    public void Fall6_UA_AusAufbauUndRaumgrenzenflaeche()
    {
        var h = Bauen();
        var p = h.P;
        var a = MaterialMitQuelle(p, "Testschicht A", 0.5);
        var b = MaterialMitQuelle(p, "Testschicht B", 0.04);
        var c = MaterialMitQuelle(p, "Testschicht C", 0.8);
        UebergangHorizontal(p, 0.13, 0.04);
        var auf = Elementkz(RaumgrenzenHilfe.Ok(HuelleTestHilfe.Aufbau(p, "Testwand", "wand", HuelleTestHilfe.Schichten((a, 100), (b, 120), (c, 80)))), "Aufbau");
        RaumgrenzenHilfe.Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject
        {
            ["elemente"] = new JsonArray(h.Sued, h.Ost, h.Nord, h.West), ["aufbau"] = auf,
        }));
        var e = RaumgrenzenHilfe.Rechnen(p);
        double u = 1.0 / 3.47;   // R = 0.2 + 3.0 + 0.1 = 3.3; U = 1/(0.13 + 3.3 + 0.04)
        var ra = Von(e, h.RA);
        // R-A Süd: U·A = (1/3.47) · 9.425 = 2.71613… W/K;  R-A West: (1/3.47) · 19.24 = 5.54467… W/K
        foreach (var (wand, flaeche) in new[] { (h.Sued, 9.425), (h.Nord, 9.425), (h.West, 19.24) })
        {
            var g = Wand(ra, wand);
            Assert.Equal(auf, g.Aufbau?.Kennzeichen);
            Nahe(u, g.U!.Value, wand + " U", 1e-9);
            Nahe(u * flaeche, g.UA!.Value, wand + " UA", 1e-9);
        }
        // Summe U·A gegen aussen von R-A = (1/3.47) · 38.09 = 10.9769… W/K
        Nahe(u * 38.09, ra.Where(x => x.Art == "wand" && x.Angrenzung == "aussen").Sum(x => x.UA!.Value), "UA R-A aussen", 1e-9);
        // Innenwand ohne Aufbau: weder U noch UA
        Assert.Null(Wand(ra, h.Innen).U);
        Assert.Null(Wand(ra, h.Innen).UA);
        // Abfrage-Summe je Angrenzung: aussen von R-A
        var o = (JsonObject)Tuer.Abfrage(p, "raumgrenzen", new JsonObject { ["raum"] = h.RA });
        var s = o["summenJeRaum"]!.AsArray().Single()!;
        Nahe(u * 38.09, s["jeAngrenzung"]!["aussen"]!["ua_WK"]!.GetValue<double>(), "ua_WK aussen (Abfrage rundet auf 0.01)", 6e-3);
        Nahe(38.09, s["jeAngrenzung"]!["aussen"]!["flaeche_m2"]!.GetValue<double>(), "flaeche_m2 aussen", 1e-3);
    }

    // ------------------------------------------------------------------ Fall 7: Wärmebrücken-Längen

    static List<Element> Wb(Projekt p) => p.Modell.DerKlasse("Waermebruecke").ToList();

    static Element WbVon(Projekt p, string art, string bauteil)
    {
        string id = ArchitekturHilfe.Hole(p, bauteil).Id;
        return Wb(p).Single(x => x.Text("art") == art && x.Bezuege.GetValueOrDefault(art == "deckenauflager" ? "wand" : "oeffnung") == id);
    }

    [Fact]
    public void Fall7_AllesBeheizt_DeckenauflagerAussenseiteUndFensteranschlag()
    {
        var h = Bauen(bBeheizt: true, oeffnungenInRA: true);
        RaumgrenzenHilfe.Ok(h.P.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        // Aussenseite samt Ecken = Aussenmass: Süd/Nord 10.00 m, Ost/West 8.00 m (Achse 9.70 / 7.70 m + 2 · 0.15 m Verlängerung)
        // Deckenauflager nur EG (Decke über EG); im OG liegt das Dach auf der Wand → keiner: 10 + 8 + 10 + 8 = 36.00 m
        Assert.Equal(10.0, WbVon(h.P, "deckenauflager", h.Sued).Zahl("laenge"));
        Assert.Equal(8.0, WbVon(h.P, "deckenauflager", h.Ost).Zahl("laenge"));
        Assert.Equal(10.0, WbVon(h.P, "deckenauflager", h.Nord).Zahl("laenge"));
        Assert.Equal(8.0, WbVon(h.P, "deckenauflager", h.West).Zahl("laenge"));
        // Fenster 1.50 × 1.20: Umfang 2 · (1.5 + 1.2) = 5.4 m; Tür 1.00 × 2.10: Umfang 2 · (1.0 + 2.1) = 6.2 m
        Assert.Equal(5.4, WbVon(h.P, "fensteranschlag", h.Fenster!).Zahl("laenge"));
        Assert.Equal(6.2, WbVon(h.P, "fensteranschlag", h.Tuer!).Zahl("laenge"));
        // 4 Deckenauflager + 2 Anschläge; Gesamtlänge 36.0 + 5.4 + 6.2 = 47.6 m
        Assert.Equal(6, Wb(h.P).Count);
        Nahe(47.6, Wb(h.P).Sum(x => x.Zahl("laenge")!.Value), "Gesamtlänge", 1e-9);
        // Lage: Deckenauflager Süd liegt auf der Aussenseite y = 0, von x = 0 bis 10000, auf der Unterkante der Decke (3000 − 250 = 2750)
        var d = WbVon(h.P, "deckenauflager", h.Sued);
        ArchitekturHilfe.Nahe([0, 0, 2750], d.Geometrie!.Punkte[0]);
        ArchitekturHilfe.Nahe([10000, 0, 2750], d.Geometrie.Punkte[1]);
        Assert.Null(d.Zahl("psi"));
    }

    [Fact]
    public void Fall7_BeiUnbeheiztemOstraum_OstwandOhneDeckenauflager_AnnahmeAusDemCode()
    {
        // Annahme (aus RaumgrenzenBefehle.cs, nicht aus der Spezifikation): Aussenwand = Wand, an der ein BEHEIZTER Raum gegen
        // aussen grenzt. Die Ostwand grenzt nur an den unbeheizten R-B → kein Deckenauflager (Hülle des beheizten Volumens).
        // Von Hand: 10.0 + 10.0 + 8.0 (Süd, Nord, West) + 5.4 + 6.2 = 39.6 m statt 47.6 m.
        var h = Bauen(bBeheizt: false, oeffnungenInRA: true);
        RaumgrenzenHilfe.Ok(h.P.Ausfuehren("waermebruecken_vorschlagen", new JsonObject()));
        Assert.Equal(5, Wb(h.P).Count);
        Assert.DoesNotContain(Wb(h.P), x => x.Bezuege.GetValueOrDefault("wand") == ArchitekturHilfe.Hole(h.P, h.Ost).Id);
        Nahe(39.6, Wb(h.P).Sum(x => x.Zahl("laenge")!.Value), "Gesamtlänge", 1e-9);
    }

    // ------------------------------------------------------------------ Fall 8: Inselraum

    [Fact]
    public void Fall8_Inselraum_UmschliessenderRaumUmDieInselVermindert_OhneUeberlappung()
    {
        var p = RaumgrenzenHilfe.Leer();
        RaumgrenzenHilfe.Geschoss(p, "EG", 0, 3000);
        RaumgrenzenHilfe.Waende(p, "EG", 100, 100, 7900, 5900, 200);     // aussen: Innenseiten 200 … 7800 × 200 … 5800
        RaumgrenzenHilfe.Waende(p, "EG", 3000, 2000, 5000, 4000, 200);   // Insel: Innenseiten 3100 … 4900 × 2100 … 3900, aussen 2900 … 5100 × 1900 … 4100
        // raum_erkennen zieht Inseln nicht ab (nur der IFC-Import, Prüfliste 12). Der umschliessende Raum wird deshalb von Hand
        // als Ring mit Steg der Breite 0 angelegt: Aussenring gegen den Uhrzeigersinn, Steg bei y = 3000 von (7800, 3000) nach
        // (5100, 3000), Insel (Aussenseite der Inselwände) im Uhrzeigersinn, Steg zurück.
        string Raum(string kz, double[][] ring)
        {
            RaumgrenzenHilfe.Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Raum", ["kennzeichen"] = kz, ["geschoss"] = "EG",
                ["eigenschaften"] = new JsonObject { ["lichteHoehe"] = 2600 },
                ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = RaumgrenzenHilfe.Pts(ring) },
            }));
            return kz;
        }
        var umgebung = Raum("R-UM", [[200, 200], [7800, 200], [7800, 3000], [5100, 3000], [5100, 1900], [2900, 1900], [2900, 4100], [5100, 4100], [5100, 3000], [7800, 3000], [7800, 5800], [200, 5800]]);
        var insel = Raum("R-IN", [[3100, 2100], [4900, 2100], [4900, 3900], [3100, 3900]]);
        var e = RaumgrenzenHilfe.Rechnen(p);
        // Insel: 1.8 × 1.8 = 3.24 m². Umgebung: 7.6 × 5.6 = 42.56 m² abzüglich der Insel samt Wänden 2.2 × 2.2 = 4.84 m² → 37.72 m²
        Nahe(3.24, e.Raeume.Single(r => r.Raum.Kennzeichen == insel).FlaecheM2, "Insel Fläche", 1e-9);
        Nahe(37.72, e.Raeume.Single(r => r.Raum.Kennzeichen == umgebung).FlaecheM2, "Umgebung Fläche", 1e-6);
        Nahe(37.72, RaumgrenzenHilfe.Summe(Von(e, umgebung), x => x.Art == "boden"), "Umgebung Boden", 1e-6);
        Nahe(3.24, RaumgrenzenHilfe.Summe(Von(e, insel), x => x.Art == "boden"), "Insel Boden", 1e-6);
        // Keine Überlappung: Fläche beider Räume + Wandfläche des Inselrings (4.84 − 3.24 = 1.60 m²) = Umgebungsfläche ohne Loch 42.56 − Hülle…
        // Summe Räume 37.72 + 3.24 = 40.96 m² = 42.56 − 1.60 (Inselwände)
        Nahe(42.56 - 1.60, e.Raeume.Sum(r => r.FlaecheM2), "Summe Räume", 1e-6);
        // Seiten der Umgebung: Aussenumfang innen 2 · (7.6 + 5.6) = 26.4 m plus Inselumfang aussen 4 · 2.2 = 8.8 m → 35.2 m · 2.60 = 91.52 m²
        Nahe(35.2 * H, RaumgrenzenHilfe.Summe(Von(e, umgebung), x => x.Art is "wand" or "fenster" or "tuer"), "Umgebung Seiten", 1e-6);
        // Seiten der Insel: Umfang innen 4 · 1.8 = 7.2 m · 2.60 = 18.72 m², Nachbar Umgebung (beheizt)
        var iw = Von(e, insel).Where(x => x.Art == "wand").ToList();
        Nahe(7.2 * H, iw.Sum(x => x.Flaeche), "Insel Seiten", 1e-6);
        Assert.All(iw, x => Assert.Equal(umgebung, x.NachbarRaum?.Kennzeichen));
        Assert.All(iw, x => Assert.Equal("beheizt", x.Angrenzung));
    }
}
