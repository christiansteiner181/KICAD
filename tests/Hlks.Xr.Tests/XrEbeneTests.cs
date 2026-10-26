// Tests Quest App P4b (Modul XR, Schnittstelle für Fachmodule): Format einer Ebene (Lesen, Prüfen, Grenzen), Geometrie in der Szene (Pfeile, Partikel, Flächen) und Laden
// vom PC mit ETag, 304 und Ablage für den Start ohne PC.

using System.Text.Json.Nodes;
using Hlks.Xr.Ebenen;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class XrEbeneTests
{
    static JsonObject Ebene(string name = "probe", params JsonObject[] elemente) => new()
    {
        ["art"] = "hlks-xr-ebene", ["fassung"] = 1, ["name"] = name, ["titel"] = "Probe", ["quelle"] = "Test", ["projekt"] = "P-1", ["schluessel"] = "abc123",
        ["einheit"] = "mm", ["elemente"] = new JsonArray([.. elemente]), ["hinweise"] = new JsonArray("ein Hinweis"),
    };

    static JsonObject El(string typ, params double[][] punkte) => new()
    {
        ["typ"] = typ, ["geschoss"] = "g-eg", ["element"] = "e-1", ["farbe"] = "#ff0000",
        ["punkte"] = new JsonArray([.. punkte.Select(p => (JsonNode)new JsonArray([.. p.Select(x => (JsonNode)x)]))]),
    };

    // ------------------------------------------------------------------------------------------------ Format

    [Fact]
    public void Lesen_alle_Typen_mit_Bezug_Farbe_und_Vorgaben()
    {
        var pfeil = El("pfeil", [0, 0, 0], [1000, 0, 0]);
        pfeil["breite"] = 15;
        var partikel = El("partikel", [0, 0, 0], [0, 2000, 0]);
        partikel["deckkraft"] = 0.5;
        var e = XrEbene.Lesen(Ebene("probe", El("linie", [0, 0, 0], [1, 2, 3]), pfeil, El("punkte", [5, 5, 5]), partikel,
            El("flaeche", [0, 0, 0], [1000, 0, 0], [1000, 1000, 0])).ToJsonString());
        Assert.Equal("probe", e.Name);
        Assert.Equal("Probe", e.Titel);
        Assert.Equal("Test", e.Quelle);
        Assert.Equal("abc123", e.Schluessel);
        Assert.Equal(["linie", "pfeil", "punkte", "partikel", "flaeche"], e.Elemente.Select(x => x.Typ));
        Assert.All(e.Elemente, x => { Assert.Equal("g-eg", x.Geschoss); Assert.Equal("e-1", x.Element); Assert.Equal(1f, x.Farbe[0]); Assert.Equal(0f, x.Farbe[1]); });
        Assert.Equal(15, e.Elemente[1].BreiteMm);
        Assert.Equal(XrEbeneFormat.BreiteVorgabe, e.Elemente[0].BreiteMm);
        Assert.Equal(XrEbeneFormat.TempoVorgabe, e.Elemente[3].TempoMmS);
        Assert.Equal(0, e.Elemente[0].TempoMmS);
        Assert.Equal(0.5f, e.Elemente[3].Farbe[3]);
        Assert.Contains(e.Hinweise, h => h.Contains("ein Hinweis"));
    }

    [Fact]
    public void Falsche_Art_Fassung_Name_Einheit_oder_JSON_werden_abgewiesen()
    {
        Assert.Throws<XrNetzFehler>(() => XrEbene.Lesen("kein json"));
        var o = Ebene(); o["art"] = "anders";
        Assert.Throws<XrNetzFehler>(() => XrEbene.Lesen(o.ToJsonString()));
        o = Ebene(); o["fassung"] = 2;
        Assert.Contains("Fassung", Assert.Throws<XrNetzFehler>(() => XrEbene.Lesen(o.ToJsonString())).Message);
        o = Ebene(); o["name"] = "../böse";
        Assert.Throws<XrNetzFehler>(() => XrEbene.Lesen(o.ToJsonString()));
        o = Ebene(); o["einheit"] = "m";
        Assert.Throws<XrNetzFehler>(() => XrEbene.Lesen(o.ToJsonString()));
    }

    [Fact]
    public void Unbekannte_Typen_und_ungueltige_Punkte_fallen_mit_Hinweis_weg()
    {
        var e = XrEbene.Lesen(Ebene("probe",
            El("wolke", [0, 0, 0]),                          // unbekannt (spätere Fassung)
            El("linie", [0, 0, 0]),                          // zu wenige Punkte
            El("flaeche", [0, 0, 0], [1, 0, 0]),            // zu wenige Punkte
            El("pfeil", [0, 0, 0], [2e8, 0, 0]),            // ausserhalb ±100 km
            El("punkte", [1, 2, 3])).ToJsonString());
        Assert.Single(e.Elemente);
        Assert.Contains(e.Hinweise, h => h.Contains("«wolke»"));
        Assert.Contains(e.Hinweise, h => h.Contains("3 Elemente ohne gültige Punkte"));
    }

    [Fact]
    public void Zu_viele_Elemente_werden_abgewiesen()
    {
        var viele = Enumerable.Range(0, XrEbeneFormat.ElementeMax + 1).Select(_ => El("punkte", [0, 0, 0])).ToArray();
        Assert.Throws<XrNetzFehler>(() => XrEbene.Lesen(Ebene("probe", viele).ToJsonString()));
    }

    [Fact]
    public void Farbe_kurz_lang_und_ungueltig()
    {
        Assert.Equal([1f, 1f, 1f, 1f], XrEbene.Farbe(null));
        Assert.Equal([1f, 1f, 1f, 0.25f], XrEbene.Farbe("rot", 0.25));
        var f = XrEbene.Farbe("#08f", 2);
        Assert.Equal(0f, f[0]);
        Nah(f[1], 0x88 / 255.0, 1e-6);
        Assert.Equal(1f, f[2]);
        Assert.Equal(1f, f[3]);
    }

    [Fact]
    public void Liste_liest_nur_gueltige_Namen_und_Adressen()
    {
        var l = EbenenListe.Lesen(new JsonObject
        {
            ["fassung"] = 1, ["projekt"] = "P-1",
            ["ebenen"] = new JsonArray(
                new JsonObject { ["name"] = "fliessrichtung", ["titel"] = "Fliessrichtung", ["url"] = "/api/xr/ebene/fliessrichtung", ["schluessel"] = "s1", ["elemente"] = 12 },
                new JsonObject { ["name"] = "boese", ["url"] = "https://anderswo/ebene" },
                new JsonObject { ["name"] = "Gross", ["url"] = "/api/xr/ebene/Gross" }),
        }.ToJsonString());
        var e = Assert.Single(l.Ebenen);
        Assert.Equal(("fliessrichtung", "Fliessrichtung", 12), (e.Name, e.Titel, e.Elemente));
        Assert.Throws<XrNetzFehler>(() => EbenenListe.Lesen("{\"fassung\":2}"));
    }

    // ------------------------------------------------------------------------------------------------ Szene

    [Fact]
    public void Bauen_rechnet_mit_dem_Bezugspunkt_in_die_Szene_um()
    {
        var e = XrEbene.Lesen(Ebene("probe", El("linie", [1000, 2000, 3500], [3000, 2000, 3500])).ToJsonString());
        var s = EbenenSzene.Bauen(e, [1000, 2000, 500]);
        var t = Assert.Single(s.Teile);
        var st = Assert.Single(t.Striche);
        Assert.Equal(V(0, 3, 0), st.A);
        Assert.Equal(V(2, 3, 0), st.B);
        Nah(t.Breite, 0.02);
        Assert.Equal(new Box3(V(0, 3, 0), V(2, 3, 0)), t.Box);
    }

    [Fact]
    public void Pfeil_hat_eine_waagrechte_Spitze_am_Ende()
    {
        var e = XrEbene.Lesen(Ebene("probe", El("pfeil", [0, 0, 0], [1000, 0, 0])).ToJsonString());
        var t = EbenenSzene.Bauen(e, null).Teile[0];
        Assert.Equal(3, t.Striche.Count);
        var spitze = t.Striche.Skip(1).ToList();
        Assert.All(spitze, s => Assert.Equal(V(1, 0, 0), s.A));
        // Länge 0.4 m (40 % des Abschnitts, höchstens 6 × 20 mm = 0.12 m, mindestens 0.08 m) → 0.12 m; halb so breit
        foreach (var s in spitze) { Nah(s.B.X, 1 - 0.12, 1e-9); Nah(s.B.Y, 0); Nah(Math.Abs(s.B.Z), 0.06, 1e-9); }
        // senkrechter Pfeil: Spitze in x aufgespannt
        var senk = EbenenSzene.Spitze(V(0, 0, 0), V(0, 1, 0), 0.02);
        Assert.All(senk, s => { Nah(s.B.Z, 0); Nah(Math.Abs(s.B.X), 0.06, 1e-9); });
    }

    [Fact]
    public void Partikel_laufen_mit_Tempo_im_Abstand_die_Bahn_entlang()
    {
        var p = El("partikel", [0, 0, 0], [3000, 0, 0], [3000, 2000, 0]);   // 3 m nach Osten, 2 m nach Norden
        p["tempo"] = 500;
        p["abstand"] = 1000;
        var t = EbenenSzene.Bauen(XrEbene.Lesen(Ebene("probe", p).ToJsonString()), null).Teile[0];
        Nah(t.BahnLaenge, 5);
        var bei0 = t.Partikel(0).ToList();
        Assert.Equal(6, bei0.Count);   // 0, 1, 2, 3, 4, 5 m
        Assert.Equal(V(0, 0, 0), bei0[0]);
        Assert.Equal(V(3, 0, -1), bei0[4]);   // 4 m: 1 m nach Norden = Szene −z
        var bei1 = t.Partikel(1).ToList();   // 0.5 m weiter
        Nah(bei1[0].X, 0.5);
        Assert.Equal(5, bei1.Count);
        Assert.Equal(bei0.Count, t.Partikel(2).Count());   // nach 2 s wieder wie am Anfang
        Assert.Equal(2, t.Partikel(0, max: 2).Count());
    }

    [Fact]
    public void Flaeche_wird_in_Dreiecke_zerlegt_auch_nicht_konvex()
    {
        // L-Form (6 Ecken) in der waagrechten Ebene: 4 Dreiecke, alle Indizes gültig, Fläche gleich der des Vielecks
        var l = new[] { V(0, 0, 0), V(2, 0, 0), V(2, 0, -1), V(1, 0, -1), V(1, 0, -2), V(0, 0, -2) };
        var d = EbenenSzene.Dreieckszerlegung(l);
        Assert.Equal(12, d.Length);
        double flaeche = 0;
        for (int i = 0; i < d.Length; i += 3)
        {
            var (a, b, c) = (l[d[i]], l[d[i + 1]], l[d[i + 2]]);
            flaeche += Math.Abs((b.X - a.X) * (c.Z - a.Z) - (c.X - a.X) * (b.Z - a.Z)) / 2;
        }
        Nah(flaeche, 3, 1e-9);
        // senkrechte Fläche (Wand) und Dreieck
        Assert.Equal(6, EbenenSzene.Dreieckszerlegung([V(0, 0, 0), V(1, 0, 0), V(1, 1, 0), V(0, 1, 0)]).Length);
        Assert.Equal([0, 1, 2], EbenenSzene.Dreieckszerlegung([V(0, 0, 0), V(1, 0, 0), V(0, 0, 1)]).Order());
        Assert.Empty(EbenenSzene.Dreieckszerlegung([V(0, 0, 0), V(1, 0, 0)]));
    }

    // ------------------------------------------------------------------------------------------------ Laden

    sealed class Quelle : IEbenenQuelle
    {
        public Dictionary<string, (string Text, string ETag)> Antworten { get; } = new(StringComparer.Ordinal);
        public List<(string Pfad, string? ETag)> Anfragen { get; } = [];
        public bool Fehlt { get; set; }

        public Task<TextAntwort> Text(string pfad, string? etag, CancellationToken abbruch)
        {
            Anfragen.Add((pfad, etag));
            if (Fehlt || !Antworten.TryGetValue(pfad, out var a)) return Task.FromResult(new TextAntwort(404, null, null));
            return Task.FromResult(etag == a.ETag ? new TextAntwort(304, null, a.ETag) : new TextAntwort(200, a.Text, a.ETag));
        }
    }

    static Quelle MitProbe(string schluessel = "s1")
    {
        var q = new Quelle();
        q.Antworten["/api/xr/ebenen"] = (new JsonObject
        {
            ["fassung"] = 1, ["projekt"] = "P-1",
            ["ebenen"] = new JsonArray(new JsonObject { ["name"] = "probe", ["titel"] = "Probe", ["url"] = "/api/xr/ebene/probe", ["schluessel"] = schluessel, ["elemente"] = 1 }),
        }.ToJsonString(), "\"liste\"");
        var e = Ebene("probe", El("punkte", [0, 0, 0]));
        e["schluessel"] = schluessel;
        q.Antworten["/api/xr/ebene/probe"] = (e.ToJsonString(), $"\"{schluessel}\"");
        return q;
    }

    [Fact]
    public async Task VomPc_laedt_legt_ab_fragt_beim_naechsten_Mal_bedingt_und_startet_ohne_PC_aus_der_Ablage()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_xr_ebenen_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ablage = new EbenenAblage(ordner);
            var q = MitProbe();
            var st = await EbenenLader.VomPc(q, "P-1", ablage);
            Assert.Equal("probe", Assert.Single(st.Ebenen).Name);
            Assert.Equal(ModellStand.VomPc, st.Quelle);
            Assert.Null(q.Anfragen[1].ETag);
            // zweites Mal: If-None-Match mit dem gespeicherten ETag, 304 → die abgelegte gilt
            q.Anfragen.Clear();
            var st2 = await EbenenLader.VomPc(q, "P-1", ablage);
            Assert.Equal("\"s1\"", q.Anfragen[1].ETag);
            Assert.Equal("probe", Assert.Single(st2.Ebenen).Name);
            // ohne PC
            var ohne = EbenenLader.AusAblage(ablage, "P-1");
            Assert.Equal("probe", Assert.Single(ohne.Ebenen).Name);
            Assert.Equal(ModellStand.AusZwischenspeicher, ohne.Quelle);
            Assert.Empty(EbenenLader.AusAblage(ablage, "P-2").Ebenen);
            // die Ebene verschwindet am PC: auch aus der Ablage
            q.Antworten["/api/xr/ebenen"] = ("{\"fassung\":1,\"projekt\":\"P-1\",\"ebenen\":[]}", "\"leer\"");
            Assert.Empty((await EbenenLader.VomPc(q, "P-1", ablage)).Ebenen);
            Assert.Empty(ablage.Namen("P-1"));
        }
        finally { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); }
    }

    [Fact]
    public async Task VomPc_ohne_Endpunkt_ist_leer_mit_Hinweis_und_ein_fehlende_Ebene_steht_in_den_Hinweisen()
    {
        var q = new Quelle { Fehlt = true };
        var st = await EbenenLader.VomPc(q, "P-1", null);
        Assert.Empty(st.Ebenen);
        Assert.Contains("älterer Stand", Assert.Single(st.Hinweise));
        // XR-Labor L1: der Labor-PC hat keine Ebenen (kein Projekt) – 404 heisst «gibt es hier nicht», ohne Hinweis (AppMarke.MitProjekt)
        var labor = await EbenenLader.VomPc(q, "P-1", null, fehlenMelden: false);
        Assert.Empty(labor.Ebenen);
        Assert.Empty(labor.Hinweise);
        var q2 = MitProbe();
        q2.Antworten.Remove("/api/xr/ebene/probe");
        var st2 = await EbenenLader.VomPc(q2, "P-1", null);
        Assert.Empty(st2.Ebenen);
        Assert.Contains(st2.Hinweise, h => h.Contains("probe") && h.Contains("404"));
    }

    [Fact]
    public void Ablage_nimmt_nur_gueltige_Namen()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_xr_ebenen_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var ablage = new EbenenAblage(ordner);
            Assert.Null(ablage.Lesen("P-1", "../../boese"));
            Assert.Empty(ablage.Namen("P-1"));
        }
        finally { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); }
    }
}
