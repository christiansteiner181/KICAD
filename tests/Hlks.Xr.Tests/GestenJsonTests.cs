// Tests der Quest App V1b-1 (Modul XR, Gestenlabor): Gestensammlung als JSON (hin und zurück, Datei, Fassung, kaputte und übergrosse Dateien) und die Regeln
// der Sammlung (eine Geste je Funktion, Stand).

using System.Text.Json.Nodes;
using Hlks.Xr.Gesten;
using G = Hlks.Xr.Tests.GestenGenerator;

namespace Hlks.Xr.Tests;

public class GestenJsonTests
{
    static GestenSammlung Sammlung() => GestenSammlung.Leer
        .MitGeste(G.Fertig(G.THebe))
        .MitGeste(G.Fertig(G.TWischLinks) with { Schwelle = 0.2, Aktiv = false })
        .MitGeste(G.Fertig(G.TKreis));

    static JsonObject Knoten(GestenSammlung s) => (JsonObject)JsonNode.Parse(GestenJson.Schreiben(s))!;

    [Fact]
    public void Hin_und_zurueck_gleich_bis_auf_die_Rundung_und_gleich_erkannt()
    {
        var s = Sammlung();
        var text = GestenJson.Schreiben(s);
        var d = GestenJson.Lesen(text);
        Assert.Empty(d.Hinweise);
        var t = d.Sammlung;
        Assert.Equal(3, t.Stand);
        Assert.Equal(s.Gesten.Select(g => (g.Id, g.Name, g.Funktion, g.Haendigkeit, g.Aktiv, g.Schwelle)),
            t.Gesten.Select(g => (g.Id, g.Name, g.Funktion, g.Haendigkeit, g.Aktiv, g.Schwelle)));
        for (var i = 0; i < s.Gesten.Count; i++)
            for (var j = 0; j < s.Gesten[i].Beispiele.Count; j++)
            {
                var (a, b) = (s.Gesten[i].Beispiele[j], t.Gesten[i].Beispiele[j]);
                Assert.Equal(a.Bilder.Count, b.Bilder.Count);
                for (var k = 0; k < a.Bilder.Count; k++)
                {
                    var (x, y) = (a.Bilder[k], b.Bilder[k]);
                    T.Nah(x.T, y.T, 1e-4);
                    T.Nah(x.Gieren, y.Gieren, 1e-5);
                    Assert.True(x.Kopf.Abstand(y.Kopf) < 1e-4);
                    foreach (var seite in new[] { HandSeite.Links, HandSeite.Rechts })
                    {
                        var (hx, hy) = (x.Hand(seite), y.Hand(seite));
                        Assert.Equal(hx.Verfolgt, hy.Verfolgt);
                        if (!hx.Verfolgt) continue;
                        Assert.True(hx.Mitte.Abstand(hy.Mitte) < 1e-4);
                        Assert.True(hx.Normale.Abstand(hy.Normale) < 1e-4);
                        T.Nah(hx.PinchAbstand, hy.PinchAbstand, 1e-4);
                        for (var g = 0; g < 25; g++) Assert.True(hx.Gelenke[g].Abstand(hy.Gelenke[g]) < 1e-4);
                    }
                }
            }
        // nach dem Runden ändert ein zweites Schreiben nichts mehr
        Assert.Equal(GestenJson.Schreiben(t), GestenJson.Schreiben(GestenJson.Lesen(GestenJson.Schreiben(t)).Sammlung));
        // gleiche Erkennung
        var vorher = new GestenErkenner(s.Gesten);
        var nachher = new GestenErkenner(t.Gesten);
        for (var i = 0; i < 3; i++)
        {
            T.Nah(vorher.Gueltige[i].Schwelle, nachher.Gueltige[i].Schwelle, 0.02 * vorher.Gueltige[i].Schwelle, "Schwelle");
            var probe = AufnahmeHilfe.Zuschneiden(G.Erzeugen(G.Auftrag[i].Bewegung, new Vorfuehrung { DauerS = G.Auftrag[i].DauerS, Saat = 55 + i }),
                G.Auftrag[i].Haendigkeit).Aufnahme!;
            var (u, v) = (vorher.Einordnen(probe), nachher.Einordnen(probe));
            Assert.Equal(u.Art, v.Art);
            Assert.Equal(u.Beste!.GestenId, v.Beste!.GestenId);
        }
        Assert.Contains("\"art\":\"hlks-xr-gesten\"", text);
        Assert.Contains("\"fassung\":1", text);
        Assert.Contains("\"haendigkeit\":\"beidhaendig\"", text);
        Assert.Contains("\"links\":null", text);
    }

    [Fact]
    public void Speichern_und_Laden_ueber_eine_Datei()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-gesten-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pfad = Path.Combine(ordner, "unter", "gesten.json");
            var s = Sammlung();
            GestenJson.Speichern(s, pfad);
            Assert.False(File.Exists(pfad + ".tmp"));
            var d = GestenJson.Laden(pfad);
            Assert.Equal(GestenJson.Schreiben(s), GestenJson.Schreiben(d.Sammlung));
            GestenJson.Speichern(s.Ohne("kreis"), pfad);   // überschreiben
            Assert.Equal(2, GestenJson.Laden(pfad).Sammlung.Gesten.Count);
            Assert.Throws<FileNotFoundException>(() => GestenJson.Laden(Path.Combine(ordner, "fehlt.json")));
        }
        finally
        {
            if (Directory.Exists(ordner)) Directory.Delete(ordner, recursive: true);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"art\":\"hlks-xr-labor-katalog\",\"fassung\":1}")]
    [InlineData("{\"art\":\"hlks-xr-gesten\"}")]
    [InlineData("{\"art\":\"hlks-xr-gesten\",\"fassung\":2,\"gesten\":[]}")]
    [InlineData("{\"art\":\"hlks-xr-gesten\",\"fassung\":\"1\",\"gesten\":[]}")]
    public void Kaputte_oder_fremde_Dateien_werfen(string text)
    {
        var f = Assert.Throws<GestenDateiFehler>(() => GestenJson.Lesen(text));
        Assert.False(string.IsNullOrWhiteSpace(f.Message));
        if (text.Contains("\"fassung\":2")) Assert.Contains("Fassung", f.Message);
    }

    [Fact]
    public void Leere_Sammlung_und_Sammlung_ohne_Gesten()
    {
        Assert.Empty(GestenJson.Lesen(GestenJson.Schreiben(GestenSammlung.Leer)).Sammlung.Gesten);
        var d = GestenJson.Lesen("{\"art\":\"hlks-xr-gesten\",\"fassung\":1}");
        Assert.Empty(d.Sammlung.Gesten);
        Assert.Equal(0, d.Sammlung.Stand);
    }

    [Fact]
    public void Ungueltige_Eintraege_fallen_mit_Hinweis_weg()
    {
        var o = Knoten(Sammlung());
        var gesten = (JsonArray)o["gesten"]!;
        JsonObject Kopie(int i) => (JsonObject)gesten[i]!.DeepClone();
        var boeseId = Kopie(0); boeseId["id"] = "Böse Id"; boeseId["funktion"] = "Boese";
        var hand = Kopie(0); hand["id"] = "hand"; hand["funktion"] = "Hand"; hand["haendigkeit"] = "dreihaendig";
        var doppelId = Kopie(1); doppelId["funktion"] = "Anders";
        var doppelFunktion = Kopie(2); doppelFunktion["id"] = "kreis-2";
        gesten.Add(boeseId); gesten.Add(hand); gesten.Add(doppelId); gesten.Add(doppelFunktion); gesten.Add(42);
        // Kreis: Beispiel 1 mit rückwärts laufender Zeit, Beispiel 2 mit 74 Gelenkwerten, Beispiel 3 mit einer Koordinate von 5 km
        var bsp = (JsonArray)gesten[2]!["beispiele"]!;
        var b0 = (JsonArray)bsp[0]!["bilder"]!;
        b0[1]!["t"] = -1;
        var b1 = (JsonArray)bsp[1]!["bilder"]!;
        ((JsonArray)b1[3]!["rechts"]!["gelenke"]!).RemoveAt(0);
        var b2 = (JsonArray)bsp[2]!["bilder"]!;
        ((JsonArray)b2[0]!["rechts"]!["mitte"]!)[0] = 5000;
        var d = GestenJson.Lesen(o.ToJsonString());
        Assert.Equal(["hebe", "wisch-links", "kreis"], d.Sammlung.Gesten.Select(g => g.Id));
        Assert.Equal(2, d.Sammlung.MitId("kreis")!.Beispiele.Count);
        Assert.Contains(d.Hinweise, h => h.Contains("Böse Id"));
        Assert.Contains(d.Hinweise, h => h.Contains("dreihaendig"));
        Assert.Contains(d.Hinweise, h => h.Contains("Id doppelt"));
        Assert.Contains(d.Hinweise, h => h.Contains("hat schon eine Geste"));
        Assert.Contains(d.Hinweise, h => h.Contains("ohne Geste"));
        Assert.Equal(3, d.Hinweise.Count(h => h.StartsWith("Geste kreis: Beispiel", StringComparison.Ordinal)));
        // die Kreis-Geste hat nun zu wenige Beispiele: der Erkenner nimmt sie nicht
        Assert.False(GestenErkenner.Vorbereiten(d.Sammlung.MitId("kreis")!).Gueltig);
    }

    [Fact]
    public void Zu_grosse_Dateien_werfen_vor_dem_Lesen_der_Einzelheiten()
    {
        var text = GestenJson.Schreiben(Sammlung());
        Assert.Throws<GestenDateiFehler>(() => GestenJson.Lesen(text, new GestenGrenzen { ZeichenMax = text.Length - 1 }));
        Assert.Contains("Gesten", Assert.Throws<GestenDateiFehler>(() => GestenJson.Lesen(text, new GestenGrenzen { GestenMax = 2 })).Message);
        Assert.Contains("Beispiele", Assert.Throws<GestenDateiFehler>(() => GestenJson.Lesen(text, new GestenGrenzen { BeispieleMax = 4 })).Message);
        Assert.Contains("Bilder", Assert.Throws<GestenDateiFehler>(() => GestenJson.Lesen(text, new GestenGrenzen { BilderJeBeispielMax = 20 })).Message);
        Assert.Contains("insgesamt", Assert.Throws<GestenDateiFehler>(() => GestenJson.Lesen(text, new GestenGrenzen { BilderGesamtMax = 200 })).Message);
        Assert.NotNull(GestenJson.Lesen(text, new GestenGrenzen { ZeichenMax = text.Length }));
        // Datei: die Grösse zählt vor dem Lesen
        var pfad = Path.Combine(Path.GetTempPath(), "hlks-gesten-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(pfad, text);
            Assert.Throws<GestenDateiFehler>(() => GestenJson.Laden(pfad, new GestenGrenzen { ZeichenMax = text.Length / 5 }));
        }
        finally { File.Delete(pfad); }
        // Vorgaben: 64 Gesten, 10 Beispiele, 400 Bilder je Beispiel, 20 000 insgesamt
        var v = GestenGrenzen.Vorgabe;
        Assert.Equal((64, 10, 400, 20_000), (v.GestenMax, v.BeispieleMax, v.BilderJeBeispielMax, v.BilderGesamtMax));
    }

    [Fact]
    public void Sammlung_eine_Geste_je_Funktion_Stand_zaehlt()
    {
        var s = Sammlung();
        Assert.Equal(3, s.Stand);
        Assert.Equal(2, s.Aktive.Count());
        // neue Geste für eine Funktion, die schon eine hat: ersetzt sie
        var neu = G.Fertig(G.TStoss) with { Funktion = G.TKreis.Funktion };
        var t = s.MitGeste(neu);
        Assert.Equal(4, t.Stand);
        Assert.Equal(3, t.Gesten.Count);
        Assert.Null(t.MitId("kreis"));
        Assert.Equal("stoss", t.FuerFunktion(G.TKreis.Funktion)!.Id);
        // gleiche Id: ersetzt
        Assert.Equal(3, t.MitGeste(neu with { Funktion = "NeueFunktion" }).Gesten.Count);
        Assert.Equal("Umbenannt", t.Umbenannt("stoss", "Umbenannt").MitId("stoss")!.Name);
        Assert.True(t.Aktiviert("wisch-links", true).MitId("wisch-links")!.Aktiv);
        Assert.Same(t, t.Aktiviert("wisch-links", false));   // unverändert
        Assert.Same(t, t.Ohne("gibt-es-nicht"));
        Assert.Equal(2, t.Ohne("stoss").Gesten.Count);
        Assert.Equal(5, t.Ohne("stoss").Stand);
        Assert.Throws<ArgumentException>(() => t.MitGeste(neu with { Id = "Böse Id" }));
        Assert.Throws<ArgumentException>(() => t.MitGeste(neu with { Funktion = "" }));
        Assert.Throws<ArgumentException>(() => t.Umbenannt("stoss", " "));
        Assert.Throws<ArgumentException>(() => t.MitGeste(neu with { Schwelle = -1 }));
    }
}
