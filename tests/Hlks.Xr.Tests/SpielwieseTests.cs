// XR-Labor Spielwiese S1: der Katalog der Platzhalter-Apps (kataloge/labor_spielwiese.json) – der echte Katalog ist gültig und hat die sechs Apps des Auftrags mit
// Farben aus der App-Pastellreihe; Grenzen und Fehler mit Stelle; Laden vom Labor-PC mit ETag/304, Ablage je Werkzeug, eingebaute Vorgabe ohne Verbindung.

using System.Text.Json.Nodes;
using Hlks.Xr.Bedienung;
using Hlks.Xr.Labor;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Tests;

public class SpielwieseTests
{
    static string Text() => File.ReadAllText(Path.Combine(T.Repo(), "kataloge", SpielwieseKatalog.Datei));
    static AppFarben Farben() => AppFarben.Lesen(File.ReadAllText(Path.Combine(T.Repo(), "kataloge", "xr_farben.json")));
    static IReadOnlyCollection<string> FarbIds() => [.. Farben().Apps.Keys];

    static string Mit(Action<JsonObject> aendern)
    {
        var o = JsonNode.Parse(Text())!.AsObject();
        aendern(o);
        return o.ToJsonString();
    }

    static JsonObject App(JsonObject k, string kennung) => k["apps"]!.AsArray().Select(a => a!.AsObject()).First(a => (string)a["kennung"]! == kennung);

    static void Falsch(string json, string stelle)
    {
        var f = Assert.Throws<XrNetzFehler>(() => SpielwieseKatalog.Lesen(json, FarbIds()));
        Assert.Contains(stelle, f.Message);
        Assert.NotNull(f.Vorschlag);
    }

    [Fact]
    public void Der_Katalog_ist_gueltig_mit_den_sechs_Apps_des_Auftrags()
    {
        var k = SpielwieseKatalog.Lesen(Text(), FarbIds());
        Assert.Empty(k.Hinweise);
        Assert.Equal(["notizen", "bilder", "uhr", "liste", "dokument", "einstellungen"], k.Apps.Select(a => a.Kennung).ToArray());
        Assert.Equal(["text", "kacheln", "uhr", "liste", "seiten", "schalter"], k.Apps.Select(a => a.Inhalt.Art).ToArray());
        // Vorschlag des Auftrags: Liste mit 40 Einträgen zum Rollen, Dokument mit 3 Seiten zum Blättern, einige Schalter, Kacheln aus Farbflächen und Formen
        Assert.Equal(40, k.App("liste")!.Inhalt.Eintraege.Count);
        Assert.Equal("Eintrag 40", k.App("liste")!.Inhalt.Eintraege[^1].Titel);
        Assert.Equal("Platzhalter 7 von 40", k.App("liste")!.Inhalt.Eintraege[6].Zeile);
        Assert.Equal(3, k.App("dokument")!.Inhalt.Seiten.Count);
        Assert.InRange(k.App("einstellungen")!.Inhalt.Schalter.Count, 3, SpielwieseKatalog.SchalterMax);
        Assert.All(k.App("bilder")!.Inhalt.Kacheln, x => Assert.Contains(x.Form, SpielwieseKatalog.Formen));
        // Symbole: Zeichen der App (keine Texturen nötig), Farben aus der App-Pastellreihe – je App eine eigene, gekennzeichnet als Vorschlag
        var farben = Farben();
        Assert.All(k.Apps, a => Assert.Contains(a.Symbol, GlasSymbole.Gezeichnet));
        Assert.Equal(k.Apps.Count, k.Apps.Select(a => a.Farbe).Distinct().Count());
        Assert.All(k.Apps, a => Assert.StartsWith("Vorschlag XR-Labor S1", farben.Apps[a.Farbe].Quelle));
        Assert.Equal(16, k.Schluessel.Length);
        Assert.Equal(k.Schluessel, SpielwieseKatalog.SchluesselVon(Text()));
    }

    [Fact]
    public void Kopf_Apps_und_Inhalte_werden_streng_gelesen_mit_der_Stelle_im_Fehler()
    {
        Falsch("{", "kein gültiges JSON");
        Falsch(Mit(k => k["art"] = "anders"), "art");
        Falsch(Mit(k => k["fassung"] = 2), "fassung");
        Falsch(Mit(k => k.Remove("beschreibung")), "beschreibung");
        Falsch(Mit(k => k["apps"] = new JsonArray()), "1 bis 11 Apps");
        Falsch(Mit(k => { var l = k["apps"]!.AsArray(); for (int i = 0; i < 6; i++) { var c = l[i]!.DeepClone(); c["kennung"] = $"x{i}"; l.Add(c); } }), "1 bis 11 Apps");
        Falsch(Mit(k => App(k, "uhr")["kennung"] = "Uhr"), "apps[2].kennung");
        Falsch(Mit(k => App(k, "uhr")["kennung"] = "notizen"), "doppelt");
        Falsch(Mit(k => App(k, "uhr")["name"] = new string('x', 25)), "apps[uhr].name höchstens 24");
        Falsch(Mit(k => App(k, "uhr")["symbol"] = "gibtsnicht"), "apps[uhr].symbol");
        Falsch(Mit(k => App(k, "uhr")["farbe"] = "rot"), "steht nicht in kataloge/xr_farben.json");
        Falsch(Mit(k => App(k, "uhr")["inhalt"]!["art"] = "film"), "apps[uhr].inhalt.art");
        Falsch(Mit(k => App(k, "notizen")["inhalt"]!["text"] = new string('a', 1201)), "höchstens 1200");
        Falsch(Mit(k => App(k, "dokument")["inhalt"]!["seiten"] = new JsonArray()), "apps[dokument].inhalt.seiten 1 bis 12");
        Falsch(Mit(k => App(k, "liste")["inhalt"]!["anzahl"] = 201), "apps[liste].inhalt.anzahl");
        Falsch(Mit(k => App(k, "liste")["inhalt"]!["muster"] = "Eintrag"), "{n}");
        Falsch(Mit(k => App(k, "einstellungen")["inhalt"]!["schalter"]![1]!["id"] = "flugmodus"), "doppelt");
        Falsch(Mit(k => App(k, "einstellungen")["inhalt"]!["schalter"]![0]!["ein"] = "ja"), "ein muss true oder false");
        Falsch(Mit(k => App(k, "bilder")["inhalt"]!["kacheln"]![0]!["farbe"] = "blau"), "#RRGGBB");
        Falsch(Mit(k => App(k, "bilder")["inhalt"]!["kacheln"]![0]!["form"] = "dreieck"), "form");
        // ohne Liste der Farben gilt jede Kennung nach Muster (der Labor-PC gibt sie mit, die App auch)
        Assert.NotNull(SpielwieseKatalog.Lesen(Mit(k => App(k, "uhr")["farbe"] = "neu"), null));
    }

    [Fact]
    public void Liste_auch_mit_Eintraegen_Textur_als_Symbol_und_unbekannte_Felder_als_Hinweis()
    {
        var json = Mit(k =>
        {
            App(k, "liste")["inhalt"] = new JsonObject { ["art"] = "liste", ["eintraege"] = new JsonArray(new JsonObject { ["titel"] = "A" }, new JsonObject { ["titel"] = "B", ["zeile"] = "zwei" }) };
            App(k, "uhr")["symbol"] = GlasSymbole.Status;   // eine Textur des Symbolsatzes
            k["neu"] = 1;
            App(k, "uhr")["inhalt"]!["format"] = "24h";
        });
        var kat = SpielwieseKatalog.Lesen(json, FarbIds());
        Assert.Equal(["A", "B"], kat.App("liste")!.Inhalt.Eintraege.Select(e => e.Titel).ToArray());
        Assert.Null(kat.App("liste")!.Inhalt.Eintraege[0].Zeile);
        Assert.Equal("info", kat.App("uhr")!.Symbol);
        Assert.Equal(["neu: unbekanntes Feld, übergangen", "apps[uhr].inhalt.format: unbekanntes Feld, übergangen"], kat.Hinweise);
        Assert.NotEqual(SpielwieseKatalog.Lesen(Text()).Schluessel, kat.Schluessel);
    }

    [Fact]
    public void Nur_das_XR_Labor_zeigt_die_Spielwiese()
    {
        Assert.True(Werkzeug.XrLabor.MitSpielwiese);
        Assert.False(Werkzeug.Kicad.MitSpielwiese);
        Assert.False(Werkzeug.Aus("hkap", "HKAP").MitSpielwiese);
        Assert.True(Werkzeug.Aus("xrlabor", "Labor am PC 2").MitSpielwiese);   // der Name aus dem Paket ändert nichts
    }

    // ------------------------------------------------------------------------------------------------ Laden vom Labor-PC, Ablage, Vorgabe

    sealed class Quelle(params TextAntwort?[] antworten) : IEbenenQuelle
    {
        int i;
        public List<(string Pfad, string? ETag)> Anfragen { get; } = [];
        public Task<TextAntwort> Text(string pfad, string? etag, CancellationToken abbruch)
        {
            Anfragen.Add((pfad, etag));
            var a = antworten[Math.Min(i++, antworten.Length - 1)];
            return a is null ? throw new HttpRequestException("Netz weg") : Task.FromResult(a);
        }
    }

    static string Ordner()
    {
        var o = Path.Combine(Path.GetTempPath(), "hlks_xr_spielwiese_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(o);
        return o;
    }

    [Fact]
    public async Task Laden_vom_PC_mit_ETag_304_Ablage_und_Vorgabe_ohne_Verbindung()
    {
        var ordner = Ordner();
        try
        {
            var ablage = new SpielwieseAblage(ordner);
            var vorgabe = Mit(k => k["apps"]!.AsArray().RemoveAt(0));   // eingebaut: fünf Apps
            var vomPc = Text();                                         // am Labor-PC: sechs Apps
            // ohne Ablage und ohne PC: die eingebaute Vorgabe
            var s0 = SpielwieseLader.OhnePc(ablage, vorgabe, FarbIds());
            Assert.Equal((SpielwieseStand.AusVorgabe, 5), (s0.Quelle, s0.Katalog!.Apps.Count));
            // 200 vom PC: gilt und wird abgelegt (mit ETag)
            var q = new Quelle(new TextAntwort(200, vomPc, "\"e1\""), new TextAntwort(304, null, "\"e1\""), null, new TextAntwort(409, "{\"fehler\":\"Katalog der Spielwiese: apps[0].name fehlt.\"}", null),
                new TextAntwort(404, null, null), new TextAntwort(200, "{\"art\":\"anders\"}", "\"e2\""));
            var s1 = await SpielwieseLader.VomPc(q, ablage, vorgabe, FarbIds());
            Assert.Equal((SpielwieseStand.VomPc, 6), (s1.Quelle, s1.Katalog!.Apps.Count));
            Assert.Equal((SpielwieseLader.Pfad, (string?)null), q.Anfragen[0]);
            Assert.Equal(("/api/xr/labor/spielwiese", (string?)null), q.Anfragen[0]);
            // 304: die Ablage, mit If-None-Match
            var s2 = await SpielwieseLader.VomPc(q, ablage, vorgabe, FarbIds());
            Assert.Equal((SpielwieseStand.Unveraendert, 6), (s2.Quelle, s2.Katalog!.Apps.Count));
            Assert.Equal("\"e1\"", q.Anfragen[1].ETag);
            // Netz weg, 409 (am PC ungültig, Meldung des PCs im Hinweis), 404 (älterer Labor-PC), unbrauchbarer Text: die zuletzt geholte Fassung
            foreach (var teil in new[] { "Netz weg", "Antwort 409 – Katalog der Spielwiese: apps[0].name fehlt", "kennt die Spielwiese noch nicht", "«hlks-labor-spielwiese»" })
            {
                var s = await SpielwieseLader.VomPc(q, ablage, vorgabe, FarbIds());
                Assert.Equal((SpielwieseStand.AusAblage, 6), (s.Quelle, s.Katalog!.Apps.Count));
                Assert.Contains(s.Hinweise, h => h.Contains(teil, StringComparison.Ordinal));
            }
            // die Ablage beim Betreten des Werkzeugs ohne PC
            Assert.Equal(SpielwieseStand.AusAblage, SpielwieseLader.OhnePc(ablage, vorgabe, FarbIds()).Quelle);
            // Ablage kaputt: die Vorgabe; Vorgabe auch kaputt: keiner
            File.WriteAllText(ablage.Datei, "{\"text\":\"kaputt\"}");
            var s5 = SpielwieseLader.OhnePc(ablage, vorgabe, FarbIds());
            Assert.Equal(SpielwieseStand.AusVorgabe, s5.Quelle);
            Assert.Contains(s5.Hinweise, h => h.StartsWith("Abgelegter Katalog der Spielwiese unbrauchbar", StringComparison.Ordinal));
            var s6 = SpielwieseLader.OhnePc(ablage, "kaputt", FarbIds());
            Assert.Equal((SpielwieseStand.Keiner, (SpielwieseKatalog?)null), (s6.Quelle, s6.Katalog));
            Assert.Equal(Path.Combine(ordner, "labor", "spielwiese.json"), ablage.Datei);
        }
        finally { Directory.Delete(ordner, true); }
    }
}
