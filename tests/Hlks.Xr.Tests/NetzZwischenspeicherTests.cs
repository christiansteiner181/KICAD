// Quest App P4a: Zwischenspeicher der GLB nach Versionsschlüssel (If-None-Match, 304), Start ohne PC (ModellLader.AusZwischenspeicher), Liste der
// Geschosse, sichere Dateinamen und Aufräumen. Die Quelle ist eine Attrappe; gegen den echten Server läuft der Integrationstest.

using System.Text.Json.Nodes;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.ModellHilfen;

namespace Hlks.Xr.Tests;

public class NetzZwischenspeicherTests
{
    /// <summary>Quelle wie der Server: Liste und GLB je Adresse mit ETag = "&lt;schluessel&gt;", 304 bei passendem If-None-Match.</summary>
    sealed class Quelle : IModellQuelle
    {
        public Dictionary<string, (byte[] Daten, string Schluessel)> Glbs { get; } = new(StringComparer.Ordinal);
        public string Liste = "";
        public List<(string Url, string? Etag, int Status)> Abrufe { get; } = [];
        public bool Netzweg;
        public string Name => "attrappe";

        public Task<string> GeschosseText(CancellationToken abbruch) =>
            Netzweg ? throw new HttpRequestException("Netz weg") : Task.FromResult(Liste);

        public Task<GlbAntwort> Glb(string url, string? etag, CancellationToken abbruch)
        {
            if (Netzweg) throw new HttpRequestException("Netz weg");
            if (!Glbs.TryGetValue(url, out var g)) throw new XrNetzFehler($"404 {url}");
            var e = $"\"{g.Schluessel}\"";
            var status = etag == e ? 304 : 200;
            Abrufe.Add((url, etag, status));
            return Task.FromResult(new GlbAntwort(status, status == 200 ? g.Daten : null, e));
        }
    }

    static byte[] KleineGlb(float x) => Glb([new GlbTeil([0, 0, 0, x, 0, 0, 0, 0, -1], null, [0, 1, 2])]);

    static string ListeText(string projekt, params (string Id, string Kz, double Kote, string Modell, string? Unterlage)[] g) => new JsonObject
    {
        ["fassung"] = 1, ["projekt"] = projekt, ["bezugspunktMm"] = new JsonArray(0, 0, 0),
        ["geschosse"] = new JsonArray([.. g.Select(x => (JsonNode)new JsonObject
        {
            ["id"] = x.Id, ["kennzeichen"] = x.Kz, ["name"] = x.Kz, ["koteMm"] = x.Kote, ["hoeheMm"] = 2800,
            ["modell"] = new JsonObject { ["schluessel"] = x.Modell, ["elemente"] = 3, ["url"] = $"/api/xr/geschoss/{x.Id}.glb?teil=modell" },
            ["unterlage"] = x.Unterlage is null ? null : new JsonObject { ["schluessel"] = x.Unterlage, ["produkte"] = 2, ["dreiecke"] = 1, ["url"] = $"/api/xr/geschoss/{x.Id}.glb?teil=unterlage" },
        })]),
    }.ToJsonString();

    static Quelle Server(string projekt = "proj-1")
    {
        var q = new Quelle { Liste = ListeText(projekt, ("g-eg", "EG", 0, "aaaaaaaa", "bbbbbbbb"), ("g-og", "1.OG", 2800, "cccccccc", null)) };
        q.Glbs["/api/xr/geschoss/g-eg.glb?teil=modell"] = (KleineGlb(1), "aaaaaaaa");
        q.Glbs["/api/xr/geschoss/g-eg.glb?teil=unterlage"] = (KleineGlb(2), "bbbbbbbb");
        q.Glbs["/api/xr/geschoss/g-og.glb?teil=modell"] = (KleineGlb(3), "cccccccc");
        return q;
    }

    [Fact]
    public void Liste_wird_gelesen_und_nur_Adressen_unter_api_xr_geschoss_angenommen()
    {
        var l = ModellListe.Lesen(ListeText("p", ("g-eg", "EG", 0, "aaaaaaaa", "bbbbbbbb")));
        Assert.Equal("p", l.Projekt);
        var g = Assert.Single(l.Geschosse);
        Assert.Equal("EG", g.Kennzeichen);
        Assert.Equal("/api/xr/geschoss/g-eg.glb?teil=unterlage", g.Unterlage!.Url);
        Assert.Equal(1, g.Unterlage.Dreiecke);
        Assert.Same(g.Modell, g.Teil("modell"));
        var boese = ListeText("p", ("g", "EG", 0, "aaaaaaaa", null)).Replace("/api/xr/geschoss/g.glb?teil=modell", "https://fremd.example/x.glb?teil=modell");
        Assert.Contains("/api/xr/geschoss/", Assert.Throws<XrNetzFehler>(() => ModellListe.Lesen(boese)).Message);
        Assert.Throws<XrNetzFehler>(() => ModellListe.Lesen("{\"geschosse\":[]}"));
        Assert.Throws<XrNetzFehler>(() => ModellListe.Lesen("kein json"));
    }

    [Fact]
    public async Task Holen_laedt_speichert_und_fragt_mit_If_None_Match()
    {
        var ordner = NetzHilfen.Ordner();
        try
        {
            var sp = new GlbZwischenspeicher(ordner);
            var q = Server();
            var l = ModellListe.Lesen(q.Liste);
            var eg = l.Geschosse[0];
            var a = await sp.Holen(q, l.Projekt, eg, "modell");
            Assert.Equal(GlbQuelle.Geladen, a.Quelle);
            Assert.Equal("aaaaaaaa", a.Schluessel);
            Assert.Equal((null, 200), (q.Abrufe[^1].Etag, q.Abrufe[^1].Status));   // nichts gespeichert: ohne Bedingung
            // gleicher Schlüssel in der Liste: aus dem Speicher, ohne Anfrage
            var b = await sp.Holen(q, l.Projekt, eg, "modell");
            Assert.Equal(GlbQuelle.Zwischenspeicher, b.Quelle);
            Assert.Single(q.Abrufe);
            Assert.Equal(a.Daten, b.Daten);
            // bedingt: If-None-Match mit dem gespeicherten ETag → 304
            var c = await sp.Holen(q, l.Projekt, eg, "modell", bedingt: true);
            Assert.Equal(GlbQuelle.Unveraendert, c.Quelle);
            Assert.Equal(("\"aaaaaaaa\"", 304), (q.Abrufe[^1].Etag, q.Abrufe[^1].Status));
            Assert.Equal(a.Daten, c.Daten);
            // neuer Schlüssel am PC (Modell geändert): die Liste nennt ihn, Anfrage mit dem alten ETag → 200, neue Datei
            q.Glbs["/api/xr/geschoss/g-eg.glb?teil=modell"] = (KleineGlb(9), "dddddddd");
            var l2 = ModellListe.Lesen(q.Liste.Replace("aaaaaaaa", "dddddddd"));
            var d = await sp.Holen(q, l2.Projekt, l2.Geschosse[0], "modell");
            Assert.Equal(GlbQuelle.Geladen, d.Quelle);
            Assert.Equal("dddddddd", d.Schluessel);
            Assert.Equal(("\"aaaaaaaa\"", 200), (q.Abrufe[^1].Etag, q.Abrufe[^1].Status));
            Assert.Equal("dddddddd", sp.Eintrag(l.Projekt, "g-eg", "modell")!.Schluessel);
            Assert.Null(sp.Lesen(l.Projekt, "g-eg", "modell", "aaaaaaaa"));   // je Geschoss und Teil nur die neueste Fassung
            // Netz weg: die gespeicherte (ältere) Fassung gilt
            q.Netzweg = true;
            var e = await sp.Holen(q, l.Projekt, eg, "modell");
            Assert.Equal(GlbQuelle.Aelter, e.Quelle);
            Assert.Equal(d.Daten, e.Daten);
            await Assert.ThrowsAsync<XrNetzFehler>(() => sp.Holen(q, l.Projekt, l.Geschosse[1], "modell"));   // nie geladen, kein Netz
        }
        finally { NetzHilfen.Weg(ordner); }
    }

    [Fact]
    public async Task Beschaedigte_Datei_wird_verworfen_und_neu_geladen()
    {
        var ordner = NetzHilfen.Ordner();
        try
        {
            var sp = new GlbZwischenspeicher(ordner);
            var q = Server();
            var l = ModellListe.Lesen(q.Liste);
            await sp.Holen(q, l.Projekt, l.Geschosse[0], "modell");
            var datei = Directory.GetFiles(ordner, "*.glb", SearchOption.AllDirectories).Single();
            File.WriteAllBytes(datei, [1, 2, 3]);
            Assert.Null(sp.Lesen(l.Projekt, "g-eg", "modell"));
            Assert.False(File.Exists(datei));
            var a = await sp.Holen(q, l.Projekt, l.Geschosse[0], "modell");
            Assert.Equal(GlbQuelle.Geladen, a.Quelle);
            Assert.Null(q.Abrufe[^1].Etag);
            Assert.True(GlbZwischenspeicher.GlbKopfGueltig(a.Daten));
            Assert.False(GlbZwischenspeicher.GlbKopfGueltig(a.Daten[..^4]));
        }
        finally { NetzHilfen.Weg(ordner); }
    }

    [Fact]
    public async Task Start_ohne_PC_aus_dem_Zwischenspeicher()
    {
        var ordner = NetzHilfen.Ordner();
        try
        {
            var sp = new GlbZwischenspeicher(ordner);
            Assert.Null(ModellLader.AusZwischenspeicher(sp));   // noch nie geladen
            var q = Server();
            var fortschritt = new List<string>();
            var vom = await ModellLader.VomPc(q, sp, new ModellLadeOptionen { Pc = "PC-TEST", Fortschritt = fortschritt.Add });
            Assert.Equal(ModellStand.VomPc, vom.Quelle);
            Assert.Equal(["EG modell", "EG unterlage", "1.OG modell"], fortschritt);
            Assert.Equal(3, vom.Teile.Count);
            Assert.All(vom.Teile, t => Assert.Equal(GlbQuelle.Geladen, t.Quelle));
            Assert.All(vom.Teile, t => Assert.True(t.Ms >= 0));
            Assert.Empty(vom.Hinweise);
            // belegter Platz: die drei GLB, die Liste und die Metadaten
            var belegt = sp.Belegt();
            Assert.True(belegt > vom.Bytes && belegt < vom.Bytes + 4096, $"{belegt} / {vom.Bytes}");
            Assert.Equal(0, new GlbZwischenspeicher(Path.Combine(ordner, "leer")).Belegt());
            // ein zweiter Start mit PC: alles aus dem Speicher, ohne neue Abrufe
            var n = q.Abrufe.Count;
            var wieder = await ModellLader.VomPc(q, sp, new ModellLadeOptionen { Pc = "PC-TEST" });
            Assert.All(wieder.Teile, t => Assert.Equal(GlbQuelle.Zwischenspeicher, t.Quelle));
            Assert.Equal(n, q.Abrufe.Count);
            // ohne PC
            var ohne = ModellLader.AusZwischenspeicher(sp)!;
            Assert.Equal(ModellStand.AusZwischenspeicher, ohne.Quelle);
            Assert.Equal("proj-1", ohne.Liste.Projekt);
            Assert.Equal(vom.Teile.Select(t => (t.Geschoss.Id, t.Teil, t.Glb.Length)), ohne.Teile.Select(t => (t.Geschoss.Id, t.Teil, t.Glb.Length)));
            Assert.Contains("Ohne PC", ohne.Hinweise[0]);
            Assert.Contains("PC-TEST", ohne.Hinweise[0]);
            Assert.Equal(2, ModellLader.AusZwischenspeicher(sp, new ModellLadeOptionen { Unterlage = false })!.Teile.Count);
            // Liste kommt nicht: VomPc wirft, der Aufrufer startet ohne PC
            q.Netzweg = true;
            await Assert.ThrowsAsync<HttpRequestException>(() => ModellLader.VomPc(q, sp));
            // fehlt eine GLB am PC, steht es in den Hinweisen; die übrigen kommen
            q.Netzweg = false;
            q.Glbs.Remove("/api/xr/geschoss/g-og.glb?teil=modell");
            Directory.Delete(Path.Combine(ordner, "modelle"), true);
            var teilweise = await ModellLader.VomPc(q, sp);
            Assert.Equal(2, teilweise.Teile.Count);
            Assert.Contains("404", Assert.Single(teilweise.Hinweise));
        }
        finally { NetzHilfen.Weg(ordner); }
    }

    [Fact]
    public async Task Aufraeumen_haelt_drei_Projekte_und_nur_Geschosse_der_Liste()
    {
        var ordner = NetzHilfen.Ordner();
        try
        {
            var sp = new GlbZwischenspeicher(ordner, projekteMax: 3);
            foreach (var p in new[] { "a", "b", "c", "d" })
            {
                await ModellLader.VomPc(Server(p), sp);
                Thread.Sleep(30);   // Zeitstempel der Ordner unterscheiden
            }
            var projekte = Directory.GetDirectories(Path.Combine(ordner, "modelle")).Select(Path.GetFileName).Order().ToList();
            Assert.Equal(["b", "c", "d"], projekte);
            Assert.Equal("d", sp.Zuletzt()!.Value.Projekt);
            // ein Geschoss fällt am PC weg: seine Dateien verschwinden beim nächsten Laden
            var q = Server("d");
            q.Liste = ListeText("d", ("g-eg", "EG", 0, "aaaaaaaa", "bbbbbbbb"));
            await ModellLader.VomPc(q, sp);
            Assert.DoesNotContain(Directory.GetFiles(Path.Combine(ordner, "modelle", "d")), f => Path.GetFileName(f).StartsWith("g-og", StringComparison.Ordinal));
        }
        finally { NetzHilfen.Weg(ordner); }
    }

    [Fact]
    public void Dateinamen_aus_dem_Netz_sind_sicher()
    {
        Assert.Equal("3f2a-11ee", GlbZwischenspeicher.Sicher("3f2a-11ee"));
        Assert.Equal("ohne-geschoss", GlbZwischenspeicher.Sicher("ohne-geschoss"));
        foreach (var boese in new[] { "../x", "..", "a/b", "a\\b", "C:", "", new string('x', 81), ".versteckt" })
            Assert.Matches("^h[0-9a-f]{16}$", GlbZwischenspeicher.Sicher(boese));
        Assert.Equal("g-eg", GlbZwischenspeicher.GeschossAusDateiname("g-eg.modell.glb"));
        Assert.Equal("1.OG", GlbZwischenspeicher.GeschossAusDateiname("1.OG.unterlage.json"));
        Assert.Null(GlbZwischenspeicher.GeschossAusDateiname("g-eg.modell.glb.1234.tmp"));
        Assert.Null(GlbZwischenspeicher.GeschossAusDateiname("liste.json"));
        Assert.Equal("abc", GlbZwischenspeicher.SchluesselAusEtag("\"abc\""));
        Assert.Equal("abc", GlbZwischenspeicher.SchluesselAusEtag("W/\"abc\""));
        Assert.Null(GlbZwischenspeicher.SchluesselAusEtag("abc"));
        Assert.Null(GlbZwischenspeicher.SchluesselAusEtag(null));
    }
}
