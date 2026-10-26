// XR-Labor L1: Ziele der Prüfaufgaben im Testraum des Labors (scene.extras.kicad.labor, Hlks.Xr.Labor.LaborZiele) – lesen und umrechnen, Wahl je Aufgabe, Wandpaar mit
// bekanntem Sollmass gegen die Prüfung der Messung, tolerantes Lesen; ohne «labor» (KICAD) keine Ziele. Dazu die Marken (Labor ohne Projekt) und ModellSzene.Extras.

using System.Text.Json.Nodes;
using Hlks.Xr.Labor;
using Hlks.Xr.Messen;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;
using P = Hlks.Xr.Labor.PruefAufgaben;

namespace Hlks.Xr.Tests;

public class LaborZieleTests
{
    static JsonArray A(params double[] w) => new([.. w.Select(x => (JsonNode)JsonValue.Create(x))]);

    /// <summary>scene.extras.kicad.labor einer Etage wie der Labor-PC (TestraumModell.Ziele), Raum 8 × 6 m ab Kote (mm), KICAD-mm.</summary>
    static JsonObject Labor(string g, double kote, bool raumAr, double soll = 6000, params (string Id, double X, double Y)[] markierungen) => new()
    {
        ["art"] = "hlks-labor-testraum", ["fassung"] = 1, ["geschoss"] = g,
        ["raeume"] = new JsonArray(new JsonObject { ["id"] = $"raum-{g}", ["kennzeichen"] = $"T.{g}", ["name"] = $"Testraum {g}", ["bodenMm"] = kote, ["raumArOeffnen"] = raumAr }),
        ["markierungen"] = new JsonArray([.. markierungen.Select(m => (JsonNode)new JsonObject { ["id"] = m.Id, ["mitteMm"] = A(m.X, m.Y, kote), ["radiusMm"] = 300 })]),
        ["wandpaare"] = new JsonArray(new JsonObject
        {
            ["id"] = $"paar-{g}", ["raum"] = $"raum-{g}", ["sollMm"] = soll,
            ["a"] = new JsonObject { ["wand"] = $"sued-{g}", ["vonMm"] = A(0, 0, kote), ["bisMm"] = A(8000, 0, kote), ["normale"] = A(0, 1, 0) },
            ["b"] = new JsonObject { ["wand"] = $"nord-{g}", ["vonMm"] = A(0, 6000, kote), ["bisMm"] = A(8000, 6000, kote), ["normale"] = A(0, -1, 0) },
        }),
        ["bauteile"] = new JsonArray(new JsonObject { ["id"] = $"ost-{g}", ["aufgabe"] = "tablet-wert-ablesen", ["hoeheMm"] = 2650 }),
    };

    static readonly (string, double, double)[] MarkenEg = [("m-sw", 2000, 1500), ("m-so", 6000, 1500), ("m-nw", 2000, 4500), ("m-no", 6000, 4500)];

    /// <summary>Raum, Wände (20 cm, bis 2.65 m) und – optional – die Ziele einer Etage in der Szene (Boden y).</summary>
    static (GlbDatei, string, string) Etage(string g, double y, JsonObject? labor)
    {
        double h = 2.65;
        var raum = ModellHilfen.RaumNetz($"raum-{g}", [new(0, 0), new(8, 0), new(8, -6), new(0, -6)], y, h);
        var waende = ModellHilfen.Quadernetz("architektur",
            ($"sued-{g}", "wand", V(-0.2, y, 0), V(8.2, y + h, 0.2)), ($"nord-{g}", "wand", V(-0.2, y, -6.2), V(8.2, y + h, -6)),
            ($"west-{g}", "wand", V(-0.2, y, -6), V(0, y + h, 0)), ($"ost-{g}", "wand", V(8, y, -6), V(8.2, y + h, 0)));
        var kicad = labor is null ? null : new JsonObject { ["teil"] = "modell", ["labor"] = labor };
        return (new GlbDatei { Netze = [raum, waende], Kicad = kicad, Bezugspunkt = [0, 0, 0] }, g, "modell");
    }

    static ModellSzene Testraum() => new([
        Etage("EG", 0, Labor("EG", 0, true, 6000, MarkenEg)),
        Etage("OG", 2.9, Labor("OG", 2900, false, 6000, ("m-og", 4000, 4800))),
    ]);

    [Fact]
    public void Ohne_labor_in_den_extras_keine_Ziele_wie_bei_KICAD()
    {
        Assert.Null(LaborZiele.AusSzene(new ModellSzene([Etage("EG", 0, null)])));
        var mitKicad = new ModellSzene([(new GlbDatei { Netze = [], Kicad = new JsonObject { ["teil"] = "modell", ["projekt"] = "P" } }, "EG", "modell")]);
        Assert.Single(mitKicad.Extras);
        Assert.Null(LaborZiele.AusSzene(mitKicad));
        // die Unterlage trägt nie Ziele
        var unterlage = new ModellSzene([(new GlbDatei { Netze = [], Kicad = new JsonObject { ["labor"] = Labor("EG", 0, true) } }, "EG", "unterlage")]);
        Assert.Null(LaborZiele.AusSzene(unterlage));
    }

    [Fact]
    public void Lesen_rechnet_KICAD_mm_in_die_Szene_um_auch_mit_Bezugspunkt()
    {
        var z = LaborZiele.AusSzene(Testraum())!;
        Assert.Equal(["EG", "OG"], z.Geschosse);
        Assert.Empty(z.Hinweise);
        Assert.Equal(5, z.Markierungen.Count);
        var sw = z.Markierungen[0];
        Assert.Equal(("m-sw", "EG"), (sw.Id, sw.GeschossId));
        Nah(sw.Mitte.X, 2); Nah(sw.Mitte.Z, -1.5); Nah(sw.Boden, 0); Nah(sw.RadiusM, 0.3);
        Nah(z.Markierungen.Single(m => m.Id == "m-og").Boden, 2.9);
        Assert.Equal("raum-EG", z.RaumArOeffnen(null));
        Assert.Equal("raum-EG", z.RaumArOeffnen("OG"));   // nur einer ist markiert
        Assert.Equal(2, z.Raeume.Count);
        Nah(z.Raeume[1].Boden, 2.9);
        Assert.Equal(2, z.Bauteile.Count);
        Nah(z.Bauteile[0].HoeheM, 2.65);
        Assert.Contains("5 Markierungen", z.Kurz);
        // mit Bezugspunkt: dieselbe Lage in der Szene
        var mitBezug = LaborZiele.Lesen([("EG", Labor("EG", 500, true, 6000, ("m", 3000, 3500)))], [1000, 2000, 500])!;
        Nah(mitBezug.Markierungen[0].Mitte.X, 2); Nah(mitBezug.Markierungen[0].Mitte.Z, -1.5); Nah(mitBezug.Markierungen[0].Boden, 0);
        Nah(mitBezug.Raeume[0].Boden, 0);
    }

    [Fact]
    public void Wandpaar_mit_Sollmass_aus_dem_Katalog_gleich_wie_die_Wahl_im_Modell()
    {
        var szene = Testraum();
        var z = LaborZiele.AusSzene(szene)!;
        var kopf = V(4, 1.7, -3);
        var ziel = z.WandPaarWaehlen(szene, "EG", kopf, 0)!;
        Assert.Equal("paar-EG", ziel.Id);
        var w = ziel.Paar;
        Assert.Equal(("raum-EG", 6000.0), (w.Raum, w.SollMm));
        Nah(w.A.Punkt.Z, 0); Nah(w.A.Normale.Z, -1);   // KICAD (0, 1, 0) = Nord = −z: in den Raum
        Nah(w.B.Punkt.Z, -6); Nah(w.B.Normale.Z, 1);
        Nah(w.A.Hoehe, WandPaare.MarkeHoeheM);
        // dieselben Wände und dasselbe Sollmass wie die Wahl im Modell (Blick nach Norden über die Wände Süd und Nord)
        var modell = WandPaare.Waehlen(szene, "EG", 0, kopf, Math.PI / 2)!;
        Nah(modell.SollMm, w.SollMm, 0.05);
        // die Prüfung der Messung: von Wand zu Wand ±2 cm gelingt, sonst Fehlgriff mit Grund
        static Messung M(Vec3 a, Vec3 b) => new(1, MessTyp.Abstand, new MessPunkt(a, Fangart.Flaeche), new MessPunkt(b, Fangart.Flaeche), XrMessLogik.Masse(a, b), "begehen", 0);
        Assert.Null(WandPaare.Pruefen(w, M(V(4, 1.2, 0), V(4, 1.2, -6))));
        Assert.Contains("nicht von Wand zu Wand", WandPaare.Pruefen(w, M(V(4, 1.2, 0), V(4, 1.2, -3)))!);
        Assert.Contains("neben dem Sollmass", WandPaare.Pruefen(w, M(V(4, 1.2, 0.03), V(4, 1.2, -6)))!);
        // OG: das Paar des OG; ausserhalb der Räume das erste der Etage; ohne Paar in der Etage null
        Nah(z.WandPaarWaehlen(szene, "OG", V(4, 4.6, -3), 2.9)!.Paar.A.Boden, 2.9);
        Assert.Equal("paar-EG", z.WandPaarWaehlen(szene, "EG", V(20, 1.7, 20), 0)!.Id);
        Assert.Null(z.WandPaarWaehlen(szene, "UG", kopf, -3));
    }

    [Fact]
    public void Markierung_im_Abstand_2_bis_4_5_m_mit_dem_kleinsten_Winkel_zum_Blick()
    {
        var z = LaborZiele.AusSzene(Testraum())!;
        var mitte = V(4, 1.7, -3);   // alle vier Markierungen 2.5 m entfernt
        // Blick nach Norden (90°): Nordwest und Nordost je 53° daneben – Gleichstand, die erste im Katalog
        Assert.Equal("m-nw", z.MarkierungWaehlen("EG", mitte, Math.PI / 2)!.Id);
        // Blick nach Osten: Südost und Nordost je 37° – Südost steht zuerst im Katalog
        Assert.Equal("m-so", z.MarkierungWaehlen("EG", mitte, 0)!.Id);
        // Blick genau auf Südwest (Gieren = atan2(−dz, dx) mit dx = −2, dz = +1.5)
        Assert.Equal("m-sw", z.MarkierungWaehlen("EG", mitte, Math.Atan2(-1.5, -2))!.Id);
        // neben Nordost (0.5 m): die zu nahe zählt nicht, solange andere im Bereich liegen
        Assert.Equal("m-nw", z.MarkierungWaehlen("EG", V(6, 1.7, -4), Math.PI / 2)!.Id);
        // keine im Bereich (weit draussen): die mit dem kleinsten Winkel
        Assert.NotNull(z.MarkierungWaehlen("EG", V(40, 1.7, -3), Math.PI));
        // je Etage; ohne Markierung in der Etage null (die App wählt dann wie ohne Testraum)
        Assert.Equal("m-og", z.MarkierungWaehlen("OG", V(4, 4.6, -3), Math.PI / 2)!.Id);
        Assert.Null(z.MarkierungWaehlen("UG", mitte, 0));
        // die Markierung liegt im Radius des Erfolgs, wenn man auf ihre Mitte springt
        var lauf = P.Starten(P.TeleportMarkierung, null);
        var m = z.MarkierungWaehlen("EG", mitte, Math.PI / 2)!;
        P.TeleportVorbereiten(lauf, m.Mitte, m.Boden);
        P.Beginnen(lauf, 0);
        P.Sprung(lauf, 1, new Xz(m.Mitte.X + 0.4, m.Mitte.Z));
        Assert.Equal(LaufZustand.Erfolg, lauf.Zustand);
    }

    [Fact]
    public void Bauteil_und_Raum_der_Aufgaben_aus_den_Zielen()
    {
        var szene = Testraum();
        var z = LaborZiele.AusSzene(szene)!;
        var e = z.BauteilWaehlen(szene, "EG")!;
        Assert.Equal("modell:ost-EG", e.Schluessel);
        var frage = P.FrageZu(e);
        Nah(frage.Soll, z.Bauteile[0].HoeheM, 1e-9);   // die Höhe der Hüllbox ist die des Katalogs
        Assert.Contains(2.65, frage.Optionen);
        Assert.Equal("modell:ost-OG", z.BauteilWaehlen(szene, "OG")!.Schluessel);
        Assert.Null(z.BauteilWaehlen(szene, "UG"));
        // ein Bauteil, das nicht in der Szene ist, zählt nicht
        var fremd = LaborZiele.Lesen([("EG", new JsonObject { ["art"] = "hlks-labor-testraum", ["fassung"] = 1, ["bauteile"] = new JsonArray(new JsonObject { ["id"] = "gibt-es-nicht", ["aufgabe"] = "tablet-wert-ablesen", ["hoeheMm"] = 1 }) })], null)!;
        Assert.Null(fremd.BauteilWaehlen(szene, "EG"));
        Assert.Null(fremd.RaumArOeffnen("EG"));
    }

    [Fact]
    public void Unbrauchbares_faellt_mit_Hinweis_weg_der_Rest_gilt()
    {
        var falsch = Labor("EG", 0, true, 6100, MarkenEg);   // Sollmass passt nicht zu den Flächen (6000 mm)
        ((JsonObject)falsch["markierungen"]![1]!).Remove("radiusMm");
        var z = LaborZiele.Lesen([("EG", falsch), ("OG", new JsonObject { ["art"] = "hlks-labor-testraum", ["fassung"] = 2 })], null)!;
        Assert.Empty(z.WandPaare);
        Assert.Equal(3, z.Markierungen.Count);
        Assert.Equal(["EG"], z.Geschosse);
        Assert.Contains(z.Hinweise, h => h.Contains("passt nicht zu seinem Sollmass") && h.Contains("6000"));
        Assert.Contains(z.Hinweise, h => h.Contains("Markierung"));
        Assert.Contains(z.Hinweise, h => h.Contains("Etage OG") && h.Contains("Fassung"));
        // Wände, die sich nicht gegenüberliegen (beide Normalen gleich)
        var gleich = Labor("EG", 0, true);
        gleich["wandpaare"]![0]!["b"]!["normale"] = A(0, 1, 0);
        Assert.Empty(LaborZiele.Lesen([("EG", gleich)], null)!.WandPaare);
    }

    [Fact]
    public void Marken_Labor_ohne_Projekt_KICAD_mit()
    {
        Assert.True(AppMarke.Kicad.MitProjekt);
        Assert.False(AppMarke.XrLabor.MitProjekt);
        Assert.Same(AppMarke.XrLabor, AppMarke.Von("xrlabor"));
    }
}
