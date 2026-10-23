// Quest App P4a: GLB lesen (GlbLeser) – Puffer, Indizes 16/32 Bit, Knoten-Transformationen, fehlende Normalen, extras (Gruppe, Bereiche, Bezugspunkt)
// und Fehler. Die Dateien schreibt eine eigene kleine Hilfe (ModellHilfen.Glb); die echten GLB von KICAD prüft der Integrationstest in Hlks.Web.Tests.

using System.Text.Json.Nodes;
using Hlks.Xr.Modell;
using static Hlks.Xr.Tests.ModellHilfen;

namespace Hlks.Xr.Tests;

public class GlbLeserTests
{
    static readonly float[] Dreieck = [0, 0, 0, 1, 0, 0, 0, 0, -1];
    static readonly float[] NachOben = [0, 1, 0, 0, 1, 0, 0, 1, 0];

    static JsonObject Kicad(JsonObject inhalt) => new() { ["kicad"] = inhalt };

    [Fact]
    public void Liest_Netz_Farbe_Gruppe_Bereiche_und_Szene()
    {
        var (pos, ind) = Quader(new Vec3(0, 0, -0.2), new Vec3(4, 2.5, 0));
        var (pos2, ind2) = Quader(new Vec3(0, 0, -3), new Vec3(0.2, 2.5, 0), 8);
        var extras = Kicad(new JsonObject
        {
            ["gruppe"] = "architektur", ["farbe"] = "#cfd3d8", ["deckkraft"] = 1.0, ["deckkraft3d"] = 0.28, ["dreiecke"] = 24,
            ["elemente"] = new JsonObject
            {
                ["ids"] = new JsonArray("w1", "w2", "kaputt"), ["ab"] = new JsonArray(0, 12, 20), ["anzahl"] = new JsonArray(12, 12, 10),
                ["ebenen"] = new JsonArray("wand", "wand", "wand"),
            },
        });
        var szene = Kicad(new JsonObject
        {
            ["teil"] = "modell", ["bezugspunktMm"] = new JsonArray(2600000.0, 1200000.0, 500.0),
            ["geschoss"] = new JsonObject { ["id"] = "g-eg", ["kennzeichen"] = "EG" },
        });
        var glb = Glb([new GlbTeil([.. pos, .. pos2], [.. Enumerable.Repeat(0f, 48)], [.. ind, .. ind2], [0.6f, 0.65f, 0.7f, 1], Extras: extras, Name: "architektur #cfd3d8 1")], szene);
        var d = GlbLeser.Lesen(glb);
        Assert.Equal("modell", d.Teil);
        Assert.Equal("g-eg", d.GeschossId);
        Assert.Equal("EG", d.GeschossKennzeichen);
        Assert.Equal([2600000.0, 1200000.0, 500.0], d.Bezugspunkt);
        var n = Assert.Single(d.Netze);
        Assert.Equal("architektur #cfd3d8 1", n.Name);
        Assert.Equal(16, n.Ecken);
        Assert.Equal(24, n.Dreiecke);
        Assert.Equal(24, d.Dreiecke);
        Assert.Equal([.. ind, .. ind2], n.Indizes);
        Assert.Equal(pos[3], n.Positionen[3]);   // Ecke 1: x = 4
        Assert.Equal("architektur", n.Gruppe);
        Assert.Equal("#cfd3d8", n.FarbeHex);
        Assert.Equal(1.0, n.Deckkraft);
        Assert.Equal(0.28, n.Deckkraft3d);
        Assert.Equal([0.6f, 0.65f, 0.7f, 1f], n.Farbe);
        Assert.False(n.Transparent);
        Assert.True(n.Doppelseitig);
        // der dritte Bereich reicht über die Dreiecke hinaus und fällt weg
        Assert.Equal([new ElementBereich("w1", 0, 12, "wand"), new ElementBereich("w2", 12, 12, "wand")], n.Bereiche);
        // StereoKit dreht beim Laden um 180° um die Hochachse (Befund P2); der eigene Leser behält die Koordinaten der GLB (x Ost, z −Nord)
        Assert.Equal(new Vec3(4, 0, -0.2), n.Ecke(1) with { Z = Math.Round(n.Ecke(1).Z, 6) });
    }

    [Fact]
    public void Indizes_32_Bit_Transparenz_und_Unterlage_mit_Entitaeten()
    {
        var extras = Kicad(new JsonObject
        {
            ["gruppe"] = "unterlage", ["deckkraft"] = 0.5,
            ["elemente"] = new JsonObject { ["ids"] = new JsonArray("2O2Fr$t4X7Zf8NOew3FLOH"), ["ab"] = new JsonArray(0), ["anzahl"] = new JsonArray(1), ["entitaeten"] = new JsonArray("IFCWALLSTANDARDCASE") },
        });
        var glb = Glb([new GlbTeil(Dreieck, NachOben, [0, 1, 2], [1, 0, 0, 0.5f], "BLEND", extras, Kurz: false)]);
        var n = Assert.Single(GlbLeser.Lesen(glb).Netze);
        Assert.True(n.Transparent);
        Assert.Equal(0.5, n.Deckkraft);
        Assert.Equal("unterlage", n.Gruppe);
        Assert.Equal("IFCWALLSTANDARDCASE", Assert.Single(n.Bereiche).Art);
    }

    [Fact]
    public void Knoten_Verschiebung_und_Matrix_werden_angewandt_Normalen_gedreht()
    {
        var glb = Glb([new GlbTeil(Dreieck, NachOben, [0, 1, 2], Translation: [10, 20, 30])]);
        var n = Assert.Single(GlbLeser.Lesen(glb).Netze);
        Assert.Equal(new Vec3(11, 20, 30), n.Ecke(1));
        // Drehung um 90° um x (glTF Spaltenreihenfolge): y → z
        float[] m = [1, 0, 0, 0, 0, 0, 1, 0, 0, -1, 0, 0, 5, 0, 0, 1];
        var r = Assert.Single(GlbLeser.Lesen(Glb([new GlbTeil(Dreieck, NachOben, [0, 1, 2], Matrix: m)])).Netze);
        T.Nah(r.Ecke(1).X, 6, 1e-6);
        T.Nah(r.Normalen[1], 0, 1e-6);
        T.Nah(r.Normalen[2], 1, 1e-6, "Normale (0, 1, 0) → (0, 0, 1)");
        T.Nah(r.Ecke(2).Y, 1, 1e-6, "Ecke (0, 0, −1) → (0, 1, 0)");
    }

    [Fact]
    public void Ohne_Normalen_und_ohne_Indizes_wird_ergaenzt()
    {
        var n = Assert.Single(GlbLeser.Lesen(Glb([new GlbTeil(Dreieck, null, [], OhneIndizes: true)])).Netze);
        Assert.Equal([0u, 1, 2], n.Indizes);
        for (int i = 0; i < 3; i++)
        {
            T.Nah(n.Normalen[3 * i], 0, 1e-6);
            T.Nah(Math.Abs(n.Normalen[3 * i + 1]), 1, 1e-6, "Fläche liegt waagrecht");
        }
        Assert.Empty(n.Bereiche);
        Assert.Equal("sonst", n.Gruppe);
    }

    [Fact]
    public void Fehler_Kennung_Laenge_Index_werden_gemeldet()
    {
        var gut = Glb([new GlbTeil(Dreieck, NachOben, [0, 1, 2])]);
        var falsch = (byte[])gut.Clone();
        falsch[0] = (byte)'x';
        Assert.Contains("glTF", Assert.Throws<GlbFehler>(() => GlbLeser.Lesen(falsch)).Message);
        Assert.Throws<GlbFehler>(() => GlbLeser.Lesen(gut[..10]));
        var kurz = gut[..^8];
        Assert.Throws<GlbFehler>(() => GlbLeser.Lesen(kurz));   // Länge im Kopf grösser als die Datei
        var index = Glb([new GlbTeil(Dreieck, NachOben, [0, 1, 7])]);
        Assert.Contains("Index 7", Assert.Throws<GlbFehler>(() => GlbLeser.Lesen(index)).Message);
    }
}
