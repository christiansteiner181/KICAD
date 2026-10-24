// Quest App P5b: der Katalog des Labors in der App – Lesen (streng beim Kopf, tolerant bei Einträgen), Prüfen der Werte nach dem Katalog, die wirksamen Werte in der
// Reihenfolge Vorgabe des Katalogs › Profil › live (LaborWerte), Grenzen aus dem Katalog, Paare, Laden vom PC mit ETag/304 und der Start ohne PC (Ablage).
// Fiktive Kataloge in der Form von GET /api/xr/labor/katalog; den echten Katalog gegen den Code prüft tests/Hlks.Web.Tests/XrLaborTests.cs.

using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Labor;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Tests;

public class LaborKatalogTests
{
    static JsonElement W(string json) => JsonDocument.Parse(json).RootElement.Clone();

    /// <summary>Ein kleiner Katalog wie vom Server: Zahl, Ja/Nein, Auswahl, Paare (Pinch), eine unbekannte Stellschraube, zwei Aufgaben, zwei Varianten, ein Profil.</summary>
    static JsonObject KatalogRoh(JsonObject? profil = null) => new()
    {
        ["art"] = "hlks-xr-labor-katalog", ["fassung"] = 1, ["schluessel"] = "abc123",
        ["gruppen"] = new JsonArray(),
        ["stellschrauben"] = new JsonArray(
            new JsonObject { ["schluessel"] = "anzeige.skala", ["art"] = "zahl", ["min"] = 1, ["max"] = 2, ["schritt"] = 0.05, ["vorgabe"] = 1.5, ["status"] = "app", ["titel"] = "Skala" },
            new JsonObject { ["schluessel"] = "apps.groesse_m", ["art"] = "zahl", ["min"] = 0.08, ["max"] = 0.6, ["schritt"] = 0.01, ["vorgabe"] = 0.2, ["status"] = "app" },
            new JsonObject { ["schluessel"] = "komfort.ton", ["art"] = "ja_nein", ["vorgabe"] = false, ["status"] = "app" },
            new JsonObject { ["schluessel"] = "apps.anordnung", ["art"] = "auswahl", ["auswahl"] = new JsonArray(new JsonObject { ["wert"] = "bogen" }, new JsonObject { ["wert"] = "reihe" }), ["vorgabe"] = "reihe", ["status"] = "app" },
            new JsonObject { ["schluessel"] = "pinch.schwelle_an_m", ["art"] = "zahl", ["min"] = 0.01, ["max"] = 0.06, ["schritt"] = 0.001, ["vorgabe"] = 0.025, ["status"] = "app" },
            new JsonObject { ["schluessel"] = "pinch.schwelle_aus_m", ["art"] = "zahl", ["min"] = 0.015, ["max"] = 0.08, ["schritt"] = 0.001, ["vorgabe"] = 0.04, ["status"] = "app" },
            new JsonObject { ["schluessel"] = "begehen.ziel_ruhe_s", ["art"] = "zahl", ["min"] = 0, ["max"] = 1, ["schritt"] = 0.05, ["vorgabe"] = 0.3, ["status"] = "app" },
            new JsonObject { ["schluessel"] = "zukunft.wert", ["art"] = "zahl", ["min"] = 0, ["max"] = 1, ["schritt"] = 0.1, ["vorgabe"] = 0.5, ["status"] = "app" },
            new JsonObject { ["schluessel"] = "kaputt.wert", ["art"] = "zahl", ["min"] = 2, ["max"] = 1, ["vorgabe"] = 1.5 },
            new JsonObject { ["schluessel"] = "farbe", ["art"] = "farbe", ["vorgabe"] = "rot" }),
        ["muster"] = new JsonArray(),
        ["messgroessen"] = new JsonArray(),
        ["aufgaben"] = new JsonArray(
            new JsonObject { ["id"] = "teleport-markierung", ["titel"] = "An eine Markierung springen", ["anleitung"] = "Spring auf die Markierung.", ["status"] = "p5",
                ["varianten"] = new JsonArray("teleport"), ["messgroessen"] = new JsonArray("zeit", "fehlgriffe", "abbrueche"), ["erfolgskriterium"] = "0.5 m", ["fehlgriff"] = "Sprung daneben", ["neu"] = 1 },
            new JsonObject { ["id"] = "messen-wand-wand", ["titel"] = "Messen", ["anleitung"] = "Miss.", ["status"] = "folgt_v1", ["varianten"] = new JsonArray("messen"),
                ["messgroessen"] = new JsonArray("zeit"), ["erfolgskriterium"] = "2 cm" }),
        ["varianten"] = new JsonArray(
            new JsonObject { ["name"] = "bedienung", ["titel"] = "Bedienung", ["optionen"] = new JsonArray(new JsonObject { ["id"] = "apps" }, new JsonObject { ["id"] = "tablet" }), ["vorgabe"] = "tablet" },
            new JsonObject { ["name"] = "teleport", ["titel"] = "Teleport", ["optionen"] = new JsonArray(new JsonObject { ["id"] = "zeigen_pinch" }, new JsonObject { ["id"] = "stick" }), ["vorgabe"] = "zeigen_pinch" },
            new JsonObject { ["name"] = "greifen", ["titel"] = "Greifen", ["optionen"] = new JsonArray(new JsonObject { ["id"] = "faust" }, new JsonObject { ["id"] = "pinch" }), ["vorgabe"] = "faust" }),
        ["profil"] = profil,
    };

    static LaborKatalog Katalog(JsonObject? profil = null) => LaborKatalog.Lesen(KatalogRoh(profil).ToJsonString());

    // ------------------------------------------------------------------------------------------------ Lesen und Prüfen

    [Fact]
    public void Lesen_streng_beim_Kopf_tolerant_bei_den_Eintraegen()
    {
        var k = Katalog();
        Assert.Equal("abc123", k.Schluessel);
        Assert.Equal(8, k.Stellschrauben.Count);                                  // «kaputt.wert» (min > max) und «farbe» (unbekannte Art) fallen weg
        Assert.Equal(2, k.Hinweise.Count);
        Assert.Equal((1.0, 2.0), k.Grenzen["anzeige.skala"]);
        Assert.Equal(StellschraubeArt.Auswahl, k.Stellschrauben["apps.anordnung"].Art);
        Assert.Equal(["bogen", "reihe"], k.Stellschrauben["apps.anordnung"].Auswahl);
        var a = k.Aufgabe("teleport-markierung")!;
        Assert.True(a.Spielbar);
        Assert.Equal("Sprung daneben", a.Fehlgriff);
        Assert.Equal(["teleport"], a.Varianten);
        Assert.False(k.Aufgabe("messen-wand-wand")!.Spielbar);
        Assert.Null(k.Aufgabe("gibt-es-nicht"));
        Assert.Equal(["zeigen_pinch", "stick"], k.Varianten["teleport"].Optionen);
        Assert.Null(k.Profil);

        Assert.Throws<XrNetzFehler>(() => LaborKatalog.Lesen("{ kein json"));
        var andereArt = KatalogRoh(); andereArt["art"] = "hlks-xr-stellschrauben";
        Assert.Throws<XrNetzFehler>(() => LaborKatalog.Lesen(andereArt.ToJsonString()));
        var andereFassung = KatalogRoh(); andereFassung["fassung"] = 2;
        Assert.Contains("Fassung", Assert.Throws<XrNetzFehler>(() => LaborKatalog.Lesen(andereFassung.ToJsonString())).Message);
        // ein leerer Katalog ist gültig (nur der Kopf)
        Assert.Empty(LaborKatalog.Lesen("""{"art":"hlks-xr-labor-katalog","fassung":1}""").Stellschrauben);
    }

    [Fact]
    public void Pruefen_nach_Art_Grenzen_Auswahl_und_Varianten()
    {
        var k = Katalog();
        Assert.Equal(WertUrteil.Ok, k.Pruefen("anzeige.skala", W("1.2")));
        Assert.Equal(WertUrteil.Ok, k.Pruefen("anzeige.skala", W("2")));          // Grenzen eingeschlossen
        Assert.Equal(WertUrteil.Ungueltig, k.Pruefen("anzeige.skala", W("2.5")));   // der Code liesse bis 3 zu – der Katalog gilt
        Assert.Equal(WertUrteil.Ungueltig, k.Pruefen("anzeige.skala", W("\"gross\"")));
        Assert.Equal(WertUrteil.Ok, k.Pruefen("anzeige.skala", W("null")));         // zurücksetzen
        Assert.Equal(WertUrteil.Ok, k.Pruefen("komfort.ton", W("true")));
        Assert.Equal(WertUrteil.Ungueltig, k.Pruefen("komfort.ton", W("1")));
        Assert.Equal(WertUrteil.Ok, k.Pruefen("apps.anordnung", W("\"bogen\"")));
        Assert.Equal(WertUrteil.Ungueltig, k.Pruefen("apps.anordnung", W("\"an_hand\"")));   // nicht in der Auswahl dieses Katalogs
        Assert.Equal(WertUrteil.Ok, k.Pruefen("labor.variante.teleport", W("\"stick\"")));
        Assert.Equal(WertUrteil.Ungueltig, k.Pruefen("labor.variante.teleport", W("\"joystick\"")));
        Assert.Equal(WertUrteil.Unbekannt, k.Pruefen("labor.variante.etage", W("\"app\"")));   // nicht in diesem Katalog
        Assert.Equal(WertUrteil.Unbekannt, k.Pruefen("x.y", W("1")));
        Assert.Equal(WertUrteil.Unbekannt, k.Pruefen("labor.aufgabe", W("\"drehen-90\"")));
    }

    // ------------------------------------------------------------------------------------------------ Wirksame Werte

    [Fact]
    public void Ohne_Katalog_gelten_die_Vorgaben_und_Grenzen_des_Codes()
    {
        var w = new LaborWerte();
        Assert.Equal(1.35, w.Bedien.Apps.Skala);
        Assert.Equal(LaborWerte.Ok, w.Setzen("anzeige.skala", W("2.8")));          // Code: bis 3
        Assert.Equal(2.8, w.Bedien.Apps.Skala);
        Assert.Equal(LaborWerte.Ungueltig, w.Setzen("anzeige.skala", W("3.5")));
        Assert.Equal(2.8, w.Bedien.Apps.Skala);                                    // der bisherige Wert gilt weiter
        Assert.Equal(LaborWerte.Ok, w.Setzen("begehen.ziel_ruhe_s", W("0.4")));
        Assert.Equal(0.4, w.Begehen.ZielRuheS);
        Assert.Equal(LaborWerte.Ok, w.Setzen("haltung.schwelle_m", W("1.2")));
        Assert.Equal(1.2, w.Haltung.SchwelleM);
        Assert.Equal(LaborWerte.Unbekannt, w.Setzen("x.y", W("1")));
        Assert.Equal(LaborWerte.Unbekannt, w.Setzen("labor.aufgabe", W("\"drehen-90\"")));   // behandelt die App selbst
        Assert.Equal(LaborWerte.Ok, w.Setzen("anzeige.skala", W("null")));
        Assert.Equal(1.35, w.Bedien.Apps.Skala);
        // Varianten, die die App kennt (auch teleport und messen, P5b)
        Assert.Equal(LaborWerte.Ok, w.Setzen("labor.variante.teleport", W("\"stick\"")));
        Assert.True(w.Bedien.MitStick);
        Assert.Equal(LaborWerte.Ok, w.Setzen("labor.variante.messen", W("\"zwei_punkte\"")));
        Assert.Equal(LaborWerte.Ungueltig, w.Setzen("labor.variante.messen", W("\"laser\"")));
        Assert.Equal(new Dictionary<string, string> { ["bedienung"] = "apps", ["teleport"] = "stick", ["etage"] = "wisch_modell", ["messen"] = "zwei_punkte", ["umgebung"] = "hell",
            ["symbole"] = "verteilt", ["symbolwahl"] = "stehen" },
            w.Varianten());
        Assert.Equal(new Dictionary<string, string> { ["teleport"] = "stick" }, w.Varianten(["teleport"]));
    }

    [Fact]
    public void Reihenfolge_Vorgabe_des_Katalogs_dann_Profil_dann_live()
    {
        var profil = new JsonObject { ["kennung"] = "christian", ["name"] = "Christian", ["werte"] = new JsonObject { ["apps.groesse_m"] = 0.3, ["labor.variante.bedienung"] = "apps" } };
        var w = new LaborWerte();
        Assert.Empty(w.KatalogSetzen(Katalog(profil)));
        Assert.Equal(1.5, w.Bedien.Apps.Skala);                                    // Vorgabe des Katalogs (Code: 1.35)
        Assert.False(w.Bedien.Komfort.Ton);
        Assert.Equal("reihe", w.Bedien.Apps.Anordnung);
        Assert.Equal(0.3, w.Begehen.ZielRuheS);
        Assert.Equal(0.3, w.Bedien.Apps.GroesseM);                                 // Profil vor der Vorgabe (0.2)
        Assert.True(w.Bedien.MitApps);                                             // Profil «apps» vor der Vorgabe der Variante («tablet»)
        Assert.Equal(LaborWerte.Ok, w.Setzen("apps.groesse_m", W("0.5")));        // live vor dem Profil; der Katalog erlaubt bis 0.6 (Code: 0.4)
        Assert.Equal(0.5, w.Bedien.Apps.GroesseM);
        Assert.Equal(LaborWerte.Ok, w.Setzen("apps.groesse_m", W("null")));       // zurück: es gilt wieder das Profil
        Assert.Equal(0.3, w.Bedien.Apps.GroesseM);
        Assert.Equal(LaborWerte.Ungueltig, w.Setzen("anzeige.skala", W("2.5")));   // Grenze des Katalogs (2)
        Assert.Equal(LaborWerte.Unbekannt, w.Setzen("zukunft.wert", W("0.2")));    // steht im Katalog, die App versteht ihn nicht
        Assert.Equal(LaborWerte.Unbekannt, w.Setzen("labor.variante.greifen", W("\"pinch\"")));
        // wirksame Werte für das Messprotokoll
        var s = w.StellschraubenWirksam();
        Assert.Equal(0.3, (double)s["apps.groesse_m"]!);
        Assert.Equal(1.5, (double)s["anzeige.skala"]!);
        Assert.Equal("apps", (string)s["labor.variante.bedienung"]!);
        Assert.False(s.ContainsKey("zukunft.wert"));
        Assert.Equal(0.3, w.Wert("apps.groesse_m")!.Value.GetDouble());
        Assert.Null(w.Wert("wisch.mindestweg_m"));                                 // keine Schicht setzt ihn: es gilt der Code
    }

    [Fact]
    public void Paare_live_ungueltig_und_neuer_Katalog_verwirft_ungueltige_Werte()
    {
        var w = new LaborWerte();
        w.KatalogSetzen(Katalog());
        Assert.Equal(LaborWerte.Ungueltig, w.Setzen("pinch.schwelle_an_m", W("0.045")));   // nicht unter «aus» (0.04)
        Assert.Equal(0.025, w.Bedien.Pinch.AnM);
        Assert.Equal(LaborWerte.Ok, w.Setzen("pinch.schwelle_aus_m", W("0.07")));
        Assert.Equal(LaborWerte.Ok, w.Setzen("pinch.schwelle_an_m", W("0.05")));
        Assert.Equal(0.05, w.Begehen.Pinch.AnM);                                   // Pinch auch im Begehen (an einer Stelle geführt)
        Assert.Equal(LaborWerte.Ok, w.Setzen("anzeige.skala", W("1.9")));
        // ein Katalog mit engeren Grenzen: der live gesetzte Wert gilt nicht mehr
        var eng = KatalogRoh();
        ((JsonObject)((JsonArray)eng["stellschrauben"]!)[0]!)["max"] = 1.6;
        Assert.Equal(["anzeige.skala"], w.KatalogSetzen(LaborKatalog.Lesen(eng.ToJsonString())));
        Assert.Equal(1.5, w.Bedien.Apps.Skala);
        // ohne Katalog: wieder die Vorgaben des Codes, live gesetzte Werte bleiben, wenn der Code sie annimmt
        w.KatalogSetzen(null);
        Assert.Equal(1.35, w.Bedien.Apps.Skala);
        Assert.Equal(0.05, w.Bedien.Pinch.AnM);
        Assert.Empty(w.Abgewiesen);
    }

    [Fact]
    public void Widerspruch_im_Profil_wird_abgewiesen_und_gemeldet()
    {
        var profil = new JsonObject { ["kennung"] = "x", ["name"] = "X", ["werte"] = new JsonObject { ["pinch.schwelle_an_m"] = 0.05 } };   // über «aus» 0.04
        var w = new LaborWerte();
        w.KatalogSetzen(Katalog(profil));
        Assert.Equal(0.025, w.Bedien.Pinch.AnM);
        Assert.Equal(["pinch.schwelle_an_m (Profil X)"], w.Abgewiesen);
    }

    // ------------------------------------------------------------------------------------------------ Laden vom PC und Ablage

    /// <summary>Quelle wie ModellHttp.Text: Antworten der Reihe nach, merkt die If-None-Match; wirft, wenn eine Antwort null ist (Netz weg).</summary>
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
        var o = Path.Combine(Path.GetTempPath(), "hlks_xr_labor_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(o);
        return o;
    }

    [Fact]
    public async Task Laden_vom_PC_mit_ETag_304_und_Ablage_fuer_den_Start_ohne_PC()
    {
        var ordner = Ordner();
        try
        {
            var ablage = new LaborKatalogAblage(ordner);
            var text = KatalogRoh().ToJsonString();
            // erster Abruf: 200, abgelegt
            var q1 = new Quelle(new TextAntwort(200, text, "\"abc123\""));
            var s1 = await LaborKatalogLader.VomPc(q1, ablage);
            Assert.Equal(LaborKatalogStand.VomPc, s1.Quelle);
            Assert.Equal("abc123", s1.Katalog!.Schluessel);
            Assert.Equal((LaborKatalogLader.Pfad, (string?)null), q1.Anfragen[0]);
            Assert.True(File.Exists(ablage.Datei));
            // zweiter Abruf: mit If-None-Match, 304 – die Ablage gilt
            var q2 = new Quelle(new TextAntwort(304, null, "\"abc123\""));
            var s2 = await LaborKatalogLader.VomPc(q2, ablage);
            Assert.Equal("\"abc123\"", q2.Anfragen[0].ETag);
            Assert.Equal(LaborKatalogStand.Unveraendert, s2.Quelle);
            Assert.Equal(8, s2.Katalog!.Stellschrauben.Count);
            // Netz weg, 404 (älteres KICAD), 500, kaputter Text: die Ablage gilt, mit Hinweis
            foreach (var q in new[] { new Quelle([null]), new Quelle(new TextAntwort(404, null, null)), new Quelle(new TextAntwort(500, "x", null)), new Quelle(new TextAntwort(200, "{ kaputt", null)) })
            {
                var s = await LaborKatalogLader.VomPc(q, ablage);
                Assert.Equal(LaborKatalogStand.AusAblage, s.Quelle);
                Assert.NotNull(s.Katalog);
                Assert.NotEmpty(s.Hinweise);
            }
            // Start ohne PC
            Assert.Equal("abc123", LaborKatalogLader.AusAblage(ablage).Katalog!.Schluessel);
            // ohne Ablage: kein Katalog (die App nimmt die Vorgaben des Codes)
            var leer = new LaborKatalogAblage(Ordner());
            var s3 = await LaborKatalogLader.VomPc(new Quelle(new TextAntwort(404, null, null)), leer);
            Assert.Equal(LaborKatalogStand.Keiner, s3.Quelle);
            Assert.Null(s3.Katalog);
            Assert.Null(LaborKatalogLader.AusAblage(leer).Katalog);
            // ein neuer Katalog (200 mit anderem Schlüssel) ersetzt die Ablage
            var neu = KatalogRoh(); neu["schluessel"] = "def456";
            await LaborKatalogLader.VomPc(new Quelle(new TextAntwort(200, neu.ToJsonString(), "\"def456\"")), ablage);
            Assert.Equal("def456", LaborKatalogLader.AusAblage(ablage).Katalog!.Schluessel);
            Assert.Equal("\"def456\"", ablage.Lesen()!.Value.ETag);
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }

    [Fact]
    public void Wartende_Messprotokolle_ueberstehen_einen_Neustart_hoechstens_50_nur_gueltige()
    {
        var ordner = Ordner();
        try
        {
            var a = new ProtokollAblage(ordner);
            Assert.Empty(a.Lesen());
            string P(int i) => KanalText.Messprotokoll(new JsonObject { ["aufgabe"] = "drehen-90", ["zeitMs"] = i, ["fehlgriffe"] = 0, ["abbrueche"] = 0, ["erfolg"] = true });
            a.Speichern([P(1), P(2), "kein json", KanalText.Tschuess()]);
            Assert.Equal([P(1), P(2)], new ProtokollAblage(ordner).Lesen());          // nur Messprotokolle; eine neue Ablage (Neustart) liest sie
            a.Speichern(Enumerable.Range(0, 60).Select(P));
            var l = a.Lesen();
            Assert.Equal(ProtokollAblage.Max, l.Count);
            Assert.Equal(P(10), l[0]);                                                // die ältesten fallen weg
            a.Speichern([]);
            Assert.False(File.Exists(a.Datei));
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }

    // ------------------------------------------------------------------------------------------------ Grenzen aus dem Katalog in den Sätzen

    [Fact]
    public void Grenzen_aus_dem_Katalog_gelten_vor_denen_des_Codes()
    {
        var weit = new Dictionary<string, (double, double)> { ["anzeige.skala"] = (0.5, 5), ["begehen.ziel_ruhe_s"] = (0, 3), ["haltung.haltezeit_s"] = (0, 20), ["roentgen.winkel_grad"] = (10, 60) };
        Assert.Null(Hlks.Xr.Bedienung.XrBedienEinstellungen.Vorgabe.MitStellschraube("anzeige.skala", W("4")));
        Assert.Equal(4, Hlks.Xr.Bedienung.XrBedienEinstellungen.Vorgabe.MitStellschraube("anzeige.skala", W("4"), weit)!.Apps.Skala);
        Assert.Null(Hlks.Xr.Bedienung.XrBedienEinstellungen.Vorgabe.MitStellschraube("roentgen.winkel_grad", W("90"), weit));   // enger als der Code (120)
        Assert.Equal(90, Hlks.Xr.Bedienung.XrBedienEinstellungen.Vorgabe.MitStellschraube("roentgen.winkel_grad", W("90"))!.Roentgen.WinkelGrad);
        Assert.Null(HandBegehenEinstellungen.Vorgabe.MitStellschraube("begehen.ziel_ruhe_s", W("2.5")));
        Assert.Equal(2.5, HandBegehenEinstellungen.Vorgabe.MitStellschraube("begehen.ziel_ruhe_s", W("2.5"), weit)!.ZielRuheS);
        Assert.Equal(15, HaltungEinstellungen.Vorgabe.MitStellschraube("haltung.haltezeit_s", W("15"), weit)!.HaltezeitS);
        Assert.Null(HaltungEinstellungen.Vorgabe.MitStellschraube("haltung.haltezeit_s", W("15")));
        Assert.Null(HandBegehenEinstellungen.Vorgabe.MitStellschraube("begehen.ziel_ruhe_s", W("\"x\""), weit));   // falscher Typ bleibt falsch
    }
}
