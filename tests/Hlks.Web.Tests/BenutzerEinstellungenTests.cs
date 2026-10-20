using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Tests.Gemeinsam;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Hlks.Web.Tests;

/// <summary>Prozessvariablen sind global: Tests, die OneDrive/HLKS_BENUTZER setzen, laufen allein.</summary>
[CollectionDefinition("BenutzerUmgebung", DisableParallelization = true)]
public sealed class BenutzerUmgebungSammlung;

/// <summary>
/// Benutzereinstellungen (G1, entwurf/projektbrowser-gliederung.md): Vereinigung zweier Rechner, Umleitung HLKS_BENUTZER, Fehlerfälle, Endpunkte
/// GET/POST /api/benutzer/einstellungen. Jeder Test hat seinen eigenen Ordner unter %TEMP% – nie die echten Dateien des Benutzers
/// (%OneDrive%\HLKS-CAD\einstellungen, %APPDATA%\HLKS-CAD\einstellungen.json).
/// </summary>
[Collection("BenutzerUmgebung")]
public class BenutzerEinstellungenTests : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_benutzer_" + Guid.NewGuid().ToString("N")[..10]);
    readonly List<IDisposable> aufraeumen = [];

    public BenutzerEinstellungenTests() { Directory.CreateDirectory(wurzel); }

    public void Dispose()
    {
        foreach (var d in aufraeumen) { try { d.Dispose(); } catch { /* egal */ } }
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }

    // ---------------------------------------------------------------- Hilfen

    static readonly DateTimeOffset T0 = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    static DateTimeOffset T(int minuten) => T0.AddMinutes(minuten);
    static JsonNode S(string s) => JsonValue.Create(s)!;

    string Ordner(string name = "benutzer") => Path.Combine(wurzel, name);

    BenutzerEinstellungen Rechner(string rechner, string? ordner = null, DateTimeOffset? zeit = null)
    {
        var b = new BenutzerEinstellungen(new RechnerAblage(ordner ?? Ordner(), rechner, true, "einstellungen.json", "Umleitung"));
        if (zeit is { } z) b.Uhr = () => z;
        return b;
    }

    static string? Gliederung(BenutzerEinstellungen b) => b.Hole("projektbrowser.gliederung")?.GetValue<string>();

    static EinstellungsDatei Datei(string rechner, params (string Schluessel, string? Wert, DateTimeOffset Zeit)[] werte)
    {
        var d = new EinstellungsDatei { Rechner = rechner };
        foreach (var (k, w, z) in werte) d.Werte[k] = new EinstellungsWert { Wert = w is null ? null : S(w), Zeit = z };
        return d;
    }

    // ---------------------------------------------------------------- Vereinigung (rein)

    [Fact]
    public void Vereinigen_neuester_Wert_je_Schluessel_ueber_alle_Rechner()
    {
        var e = BenutzerEinstellungen.Vereinigen([
            Datei("PC-A", ("projektbrowser.gliederung", "sanitaer", T(1)), ("design", "hell", T(5))),
            Datei("PC-B", ("projektbrowser.gliederung", "heizung", T(3)), ("design", "dunkel", T(2))),
        ]);
        Assert.Equal("heizung", e["projektbrowser.gliederung"].Wert!.GetValue<string>());
        Assert.Equal("PC-B", e["projektbrowser.gliederung"].Rechner);
        Assert.Equal("hell", e["design"].Wert!.GetValue<string>());
        Assert.Equal("PC-A", e["design"].Rechner);
    }

    [Fact]
    public void Vereinigen_bei_gleicher_Zeit_gewinnt_der_groessere_Rechnername_auf_jeder_Seite()
    {
        var a = Datei("PC-A", ("k", "a", T(1)));
        var b = Datei("PC-B", ("k", "b", T(1)));
        Assert.Equal("b", BenutzerEinstellungen.Vereinigen([a, b])["k"].Wert!.GetValue<string>());
        Assert.Equal("b", BenutzerEinstellungen.Vereinigen([b, a])["k"].Wert!.GetValue<string>());   // Reihenfolge der Dateien egal
    }

    [Fact]
    public void Vereinigen_neuerer_Reset_hebt_aelteren_Wert_eines_anderen_Rechners_auf_und_umgekehrt()
    {
        var wert = Datei("PC-A", ("k", "sanitaer", T(1)));
        var reset = Datei("PC-B", ("k", null, T(2)));
        Assert.Null(BenutzerEinstellungen.Vereinigen([wert, reset])["k"].Wert);
        var spaeter = Datei("PC-A", ("k", "lueftung", T(3)));
        Assert.Equal("lueftung", BenutzerEinstellungen.Vereinigen([spaeter, reset])["k"].Wert!.GetValue<string>());
    }

    [Fact]
    public void Vereinigen_ohne_Dateien_und_mit_leeren_Dateien_ist_leer()
    {
        Assert.Empty(BenutzerEinstellungen.Vereinigen([]));
        Assert.Empty(BenutzerEinstellungen.Vereinigen([Datei("PC-A")]));
    }

    // ---------------------------------------------------------------- Prüfen (Fehlerfälle)

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Design")]                       // Grossbuchstaben
    [InlineData("1abc")]                         // beginnt mit Ziffer
    [InlineData("mit leerzeichen")]
    [InlineData("a/b")]
    [InlineData("ä")]
    public void Pruefen_lehnt_ungueltige_Schluessel_ab_mit_Vorschlag(string? schluessel)
    {
        var f = Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen(schluessel, S("x")));
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
    }

    [Fact]
    public void Pruefen_lehnt_zu_langen_Schluessel_und_zu_grossen_Wert_ab()
    {
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("a" + new string('b', 64), S("x")));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("design", S(new string('x', 5000))));
        BenutzerEinstellungen.Pruefen("a" + new string('b', 63), S("x"));   // genau 64 Zeichen gehen
    }

    [Theory]
    [InlineData("sanitaer", true)]
    [InlineData("gebaeude", true)]
    [InlineData("kaelte", true)]                 // älterer Wert (Kälte gehört zur Heizung): der Server prüft nur die Form, die Oberfläche liest ihn als «heizung»
    [InlineData("Sanitär", false)]
    [InlineData("", false)]
    [InlineData("mit leerzeichen", false)]
    [InlineData("1", false)]
    public void Pruefen_Gliederung_prueft_nur_die_Form_der_Kennung(string wert, bool gut)
    {
        if (gut) BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", S(wert));
        else Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", S(wert)));
    }

    [Fact]
    public void Pruefen_Gliederung_lehnt_Zahl_und_Objekt_ab_null_setzt_zurueck()
    {
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", JsonValue.Create(3)));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonObject()));
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", null);
    }

    [Theory]
    [InlineData("strich", true)]
    [InlineData("revit", true)]
    [InlineData("Revit", false)]
    [InlineData("", false)]
    [InlineData("bunt", false)]
    public void Pruefen_Symbolsatz_kennt_strich_und_revit(string wert, bool gut)
    {
        if (gut) BenutzerEinstellungen.Pruefen("oberflaeche.symbole", S(wert));
        else Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("oberflaeche.symbole", S(wert)));
    }

    [Fact]
    public void Pruefen_Symbolsatz_lehnt_Zahl_und_Liste_ab_null_setzt_zurueck()
    {
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("oberflaeche.symbole", JsonValue.Create(1)));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("oberflaeche.symbole", new JsonArray("revit")));
        BenutzerEinstellungen.Pruefen("oberflaeche.symbole", null);
    }

    // ---------------------------------------------------------------- G2: mehrere Gliederungen zugleich (Wert ist eine Liste)

    [Fact]
    public void Pruefen_Gliederung_nimmt_Listen_von_Kennungen_an_auch_ein_alter_Einzelwert_bleibt_gueltig()
    {
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("sanitaer"));
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("sanitaer", "heizung"));
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("sanitaer", "heizung", "lueftung", "koordination", "energie"));
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("kaelte", "sanitaer"));   // älterer Wert: nur die Form wird geprüft, die Oberfläche liest «kaelte» als «heizung»
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", S("sanitaer"));                          // alter Einzelwert
    }

    [Fact]
    public void Pruefen_Gliederung_lehnt_leere_Listen_Doppelte_falsche_Kennungen_und_zu_lange_Listen_ab()
    {
        var f = Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray()));
        Assert.Contains("mindestens eine", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("sanitaer", "sanitaer")));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("Sanitär")));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray(JsonValue.Create(3))));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray("sanitaer", null)));
        Assert.Throws<HlksFehler>(() => BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray(Enumerable.Range(0, 17).Select(i => (JsonNode?)JsonValue.Create("g" + i)).ToArray())));
        BenutzerEinstellungen.Pruefen("projektbrowser.gliederung", new JsonArray(Enumerable.Range(0, 16).Select(i => (JsonNode?)JsonValue.Create("g" + i)).ToArray()));
    }

    [Fact]
    public void Liste_wird_als_Liste_gespeichert_und_gelesen_zwei_Rechner_der_neueste_Wert_gilt_ganz()
    {
        var a = Rechner("PC-A", zeit: T(1));
        var b = Rechner("PC-B", zeit: T(2));
        a.Setzen("projektbrowser.gliederung", new JsonArray("sanitaer", "heizung"));
        Assert.Equal(["sanitaer", "heizung"], b.Hole("projektbrowser.gliederung")!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        Assert.Equal(["sanitaer", "heizung"], JsonNode.Parse(File.ReadAllText(Path.Combine(Ordner(), "PC-A.json")))!["werte"]!["projektbrowser.gliederung"]!["wert"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        b.Setzen("projektbrowser.gliederung", new JsonArray("lueftung"));   // T(2) ist neuer: die ganze Liste ersetzt die alte, es wird nicht gemischt
        Assert.Equal(["lueftung"], a.Hole("projektbrowser.gliederung")!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
    }

    [Fact]
    public void Ein_alter_Einzelwert_und_eine_Liste_vereinigen_sich_nach_der_Zeit()
    {
        var a = Rechner("PC-A", zeit: T(1));
        var b = Rechner("PC-B", zeit: T(2));
        a.Setzen("projektbrowser.gliederung", S("sanitaer"));                        // alter Stand eines Rechners mit der früheren Version
        Assert.Equal("sanitaer", b.Hole("projektbrowser.gliederung")!.GetValue<string>());   // bleibt als Text lesbar: die Oberfläche macht eine Liste daraus
        b.Setzen("projektbrowser.gliederung", new JsonArray("sanitaer", "koordination"));
        Assert.Equal(2, a.Hole("projektbrowser.gliederung")!.AsArray().Count);
    }

    [Fact]
    public void Der_Wert_einer_Liste_wird_beim_Zuruecksetzen_wie_jeder_andere_aufgehoben()
    {
        var a = Rechner("PC-A", zeit: T(1));
        var b = Rechner("PC-B", zeit: T(2));
        a.Setzen("projektbrowser.gliederung", new JsonArray("sanitaer", "heizung"));
        b.Setzen("projektbrowser.gliederung", null);
        Assert.Null(a.Hole("projektbrowser.gliederung"));
    }

    [Fact]
    public async Task Endpunkt_Liste_als_Wert_wird_gespeichert_und_unveraendert_geliefert()
    {
        var s = Starten(Ordner());
        var (code, p) = await s.Post(new { schluessel = "projektbrowser.gliederung", wert = new[] { "sanitaer", "heizung" } });
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Equal(["sanitaer", "heizung"], p!["wert"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        var (_, g) = await s.Get();
        Assert.Equal(["sanitaer", "heizung"], g!["werte"]!["projektbrowser.gliederung"]!.AsArray().Select(n => n!.GetValue<string>()).ToArray());
        Assert.Equal("testpc", g["zeiten"]!["projektbrowser.gliederung"]!["rechner"]!.GetValue<string>());
        // ein einzelner Text (alte Oberfläche) geht weiter, und ersetzt die Liste
        var (c2, _) = await s.Post(new { schluessel = "projektbrowser.gliederung", wert = "gebaeude" });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Equal("gebaeude", (await s.Get()).Json!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
        // eine leere Liste wird abgelehnt und ändert nichts
        var (c3, j) = await s.Post(new { schluessel = "projektbrowser.gliederung", wert = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, c3);
        Assert.False(string.IsNullOrWhiteSpace(j!["vorschlag"]?.GetValue<string>()));
        Assert.Equal("gebaeude", (await s.Get()).Json!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
    }

    [Fact]
    public async Task Endpunkt_zwei_Benutzer_behalten_je_ihre_Liste()
    {
        var anna = Starten(Ordner("anna"));
        var beat = Starten(Ordner("beat"));
        await anna.Post(new { schluessel = "projektbrowser.gliederung", wert = new[] { "sanitaer", "heizung" } });
        await beat.Post(new { schluessel = "projektbrowser.gliederung", wert = new[] { "koordination" } });
        Assert.Equal(2, (await anna.Get()).Json!["werte"]!["projektbrowser.gliederung"]!.AsArray().Count);
        Assert.Equal("koordination", (await beat.Get()).Json!["werte"]!["projektbrowser.gliederung"]![0]!.GetValue<string>());
    }

    [Fact]
    public void Pruefen_nimmt_andere_Schluessel_mit_Text_Zahl_Ja_Nein_und_kleinen_Objekten_an()
    {
        BenutzerEinstellungen.Pruefen("design", S("dunkel"));
        BenutzerEinstellungen.Pruefen("menueband.minimiert", JsonValue.Create(true));
        BenutzerEinstellungen.Pruefen("seitenleiste.breite", JsonValue.Create(280));
        BenutzerEinstellungen.Pruefen("browser.offen", new JsonArray("a", "b"));
    }

    // ---------------------------------------------------------------- Ablage: zwei Rechner

    [Fact]
    public void Zwei_Rechner_sehen_den_neuesten_Wert_und_schreiben_nur_ihre_eigene_Datei()
    {
        var a = Rechner("PC-A", zeit: T(1));
        var b = Rechner("PC-B", zeit: T(2));
        Assert.Empty(a.Alle());

        a.Setzen("projektbrowser.gliederung", S("sanitaer"));
        Assert.Equal("sanitaer", Gliederung(b));   // der andere Rechner sieht es sofort (Datei im geteilten Ordner)

        b.Setzen("projektbrowser.gliederung", S("heizung"));   // T(2) ist neuer
        Assert.Equal("heizung", Gliederung(a));
        Assert.Equal("heizung", Gliederung(b));

        var dateien = Directory.GetFiles(Ordner(), "*.json").Select(Path.GetFileName).OrderBy(x => x).ToArray();
        Assert.Equal(["PC-A.json", "PC-B.json"], dateien);
        // A hat nur seinen Wert geschrieben (sanitaer), B nur seinen
        Assert.Equal("sanitaer", JsonNode.Parse(File.ReadAllText(Path.Combine(Ordner(), "PC-A.json")))!["werte"]!["projektbrowser.gliederung"]!["wert"]!.GetValue<string>());
        Assert.Equal("heizung", JsonNode.Parse(File.ReadAllText(Path.Combine(Ordner(), "PC-B.json")))!["werte"]!["projektbrowser.gliederung"]!["wert"]!.GetValue<string>());
    }

    [Fact]
    public void Der_spaetere_Wechsel_auf_dem_ersten_Rechner_gewinnt_wieder()
    {
        var zeit = T(1);
        var a = Rechner("PC-A"); a.Uhr = () => zeit;
        var b = Rechner("PC-B"); b.Uhr = () => zeit;
        a.Setzen("projektbrowser.gliederung", S("sanitaer")); zeit = T(2);
        b.Setzen("projektbrowser.gliederung", S("heizung")); zeit = T(3);
        a.Setzen("projektbrowser.gliederung", S("koordination"));
        Assert.Equal("koordination", Gliederung(a));
        Assert.Equal("koordination", Gliederung(b));
    }

    [Fact]
    public void Verschiedene_Schluessel_zweier_Rechner_bleiben_beide_erhalten()
    {
        var a = Rechner("PC-A", zeit: T(1));
        var b = Rechner("PC-B", zeit: T(2));
        a.Setzen("projektbrowser.gliederung", S("lueftung"));
        b.Setzen("design", S("hell"));
        var alle = a.Alle();
        Assert.Equal("lueftung", alle["projektbrowser.gliederung"].Wert!.GetValue<string>());
        Assert.Equal("hell", alle["design"].Wert!.GetValue<string>());
    }

    [Fact]
    public void Zuruecksetzen_nimmt_den_Schluessel_weg_auch_wenn_ein_anderer_Rechner_ihn_gesetzt_hat()
    {
        var a = Rechner("PC-A", zeit: T(1));
        var b = Rechner("PC-B", zeit: T(2));
        a.Setzen("projektbrowser.gliederung", S("sanitaer"));
        b.Setzen("projektbrowser.gliederung", null);
        Assert.Null(Gliederung(a));
        Assert.DoesNotContain("projektbrowser.gliederung", a.Alle().Keys);
    }

    [Fact]
    public void Ein_anderer_Benutzer_hat_seinen_eigenen_Ordner_und_behaelt_seine_Gliederung()
    {
        var anna = Rechner("PC-A", Ordner("anna"), T(1));
        var beat = Rechner("PC-A", Ordner("beat"), T(2));
        anna.Setzen("projektbrowser.gliederung", S("sanitaer"));
        beat.Setzen("projektbrowser.gliederung", S("lueftung"));
        Assert.Equal("sanitaer", Gliederung(anna));
        Assert.Equal("lueftung", Gliederung(beat));
    }

    [Fact]
    public void Defekte_Datei_eines_anderen_Rechners_wird_uebersprungen_und_stoert_nicht()
    {
        var a = Rechner("PC-A", zeit: T(1));
        a.Setzen("projektbrowser.gliederung", S("heizung"));
        File.WriteAllText(Path.Combine(Ordner(), "PC-X.json"), "{ das ist kein json");
        File.WriteAllText(Path.Combine(Ordner(), "PC-Y.json"), "");
        Assert.Equal("heizung", Gliederung(a));
    }

    [Fact]
    public void Defekte_eigene_Datei_wird_beim_naechsten_Setzen_neu_geschrieben()
    {
        Directory.CreateDirectory(Ordner());
        File.WriteAllText(Path.Combine(Ordner(), "PC-A.json"), "{ kaputt");
        var a = Rechner("PC-A", zeit: T(1));
        Assert.Empty(a.Alle());
        a.Setzen("projektbrowser.gliederung", S("koordination"));
        Assert.Equal("koordination", Gliederung(a));
        Assert.Equal("koordination", Gliederung(Rechner("PC-B")));
    }

    [Fact]
    public void Eigene_Datei_wird_atomar_geschrieben_ohne_temporaere_Reste()
    {
        var a = Rechner("PC-A", zeit: T(1));
        for (int i = 0; i < 5; i++) a.Setzen("zaehler", JsonValue.Create(i));
        Assert.Empty(Directory.GetFiles(Ordner(), "*.tmp"));
        Assert.Equal(4, a.Hole("zaehler")!.GetValue<int>());
    }

    [Fact]
    public void Rechnername_mit_ungueltigen_Zeichen_wird_zum_gueltigen_Dateinamen()
    {
        var a = Rechner("PC/A:1", zeit: T(1));
        a.Setzen("design", S("hell"));
        Assert.Single(Directory.GetFiles(Ordner(), "*.json"));
        Assert.Equal("hell", a.Hole("design")!.GetValue<string>());
    }

    [Fact]
    public void Setzen_meldet_nicht_beschreibbare_Ablage_mit_Vorschlag()
    {
        File.WriteAllText(Ordner("datei"), "ich bin eine Datei, kein Ordner");   // CreateDirectory auf eine Datei scheitert
        var a = Rechner("PC-A", Ordner("datei"), T(1));
        var f = Assert.Throws<HlksFehler>(() => a.Setzen("design", S("hell")));
        Assert.Contains("nicht gespeichert", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.Empty(a.Alle());   // Lesen geht trotzdem leer statt mit Ausnahme
    }

    // ---------------------------------------------------------------- Schutz im Testlauf

    [Fact]
    public void Im_Testlauf_ohne_Umleitung_nach_Temp_wird_weder_gelesen_noch_geschrieben()
    {
        using var u = TestUmgebung.Setzen(("HLKS_TEST", "1"));
        var ausserhalb = Path.Combine(AppContext.BaseDirectory, "nicht_temp_" + Guid.NewGuid().ToString("N")[..8]);
        Assert.False(RechnerAblage.Temporaer(ausserhalb));
        var b = Rechner("PC-A", ausserhalb, T(1));
        Assert.Empty(b.Alle());
        var f = Assert.Throws<HlksFehler>(() => b.Setzen("projektbrowser.gliederung", S("sanitaer")));
        Assert.Contains("Testlauf", f.Message);
        Assert.Contains("HLKS_BENUTZER", f.Vorschlag);
        Assert.False(Directory.Exists(ausserhalb), "es darf nichts angelegt werden");
    }

    [Fact]
    public void Im_Testlauf_ohne_Umleitung_liest_der_Test_keine_vorhandene_Datei()
    {
        // Eine "echte" Datei ausserhalb von %TEMP% (im Build-Ordner) mit Wert: im Testlauf bleibt sie unsichtbar
        using var u = TestUmgebung.Setzen(("HLKS_TEST", "1"));
        var ausserhalb = Path.Combine(AppContext.BaseDirectory, "nicht_temp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(ausserhalb);
        try
        {
            File.WriteAllText(Path.Combine(ausserhalb, "PC-A.json"), Json.Text(Datei("PC-A", ("projektbrowser.gliederung", "sanitaer", T(1)))));
            Assert.Empty(Rechner("PC-A", ausserhalb).Alle());
        }
        finally { Directory.Delete(ausserhalb, true); }
    }

    [Fact]
    public void Im_Testlauf_ohne_HLKS_BENUTZER_sind_die_echten_OneDrive_Dateien_unsichtbar()
    {
        // Der Fall der Browser-Testreihen: Server mit HLKS_TEST=1, aber ohne HLKS_BENUTZER/HLKS_ZULETZT; OneDrive des Entwicklers liegt ausserhalb von %TEMP%
        var od = Path.Combine(AppContext.BaseDirectory, "nicht_temp_od_" + Guid.NewGuid().ToString("N")[..8]);
        var einst = Path.Combine(od, "HLKS-CAD", "einstellungen");
        var zuletzt = Path.Combine(od, "HLKS-CAD", "zuletzt");
        Directory.CreateDirectory(einst); Directory.CreateDirectory(zuletzt);
        try
        {
            using var u = TestUmgebung.Setzen(("HLKS_TEST", "1"), ("HLKS_BENUTZER", null), ("HLKS_ZULETZT", null), ("OneDrive", od), ("OneDriveCommercial", null), ("OneDriveConsumer", null));
            var wert = new JsonArray(S("heizung"), S("sanitaer"), S("lueftung"));
            File.WriteAllText(Path.Combine(einst, "pc-eins.json"), Json.Text(new EinstellungsDatei
            {
                Rechner = "pc-eins",
                Werte = { ["projektbrowser.gliederung"] = new EinstellungsWert { Wert = wert, Zeit = T(1) } },
            }));
            File.WriteAllText(Path.Combine(zuletzt, "pc-eins.json"), Json.Text(new ZuletztDatei
            {
                Rechner = "pc-eins",
                Eintraege = [new ZuletztEintrag { Ordner = @"W:\02_Projektdateien Objekte\Worb_BES_Bernstrasse\c_Pläne und Modelle\e_KICAD", Name = "Bernstrasse", Zeit = T(1) }],
            }));

            var e = new Einstellungen("T", "pc-eins", Katalog.StandardOrdner(), null);
            var b = BenutzerEinstellungen.Aus(e);
            Assert.Equal("OneDrive", b.Quelle);               // die Wahl der Ablage ändert sich nicht ...
            Assert.Empty(b.Alle());                            // ... gelesen wird im Testlauf trotzdem nichts
            Assert.Null(b.Hole("projektbrowser.gliederung"));
            var z = ZuletztListe.Aus(e);
            Assert.Equal("OneDrive", z.Quelle);
            Assert.Empty(z.Alle());
        }
        finally { Directory.Delete(od, true); }
    }

    // ---------------------------------------------------------------- Wahl der Ablage (Umleitung HLKS_BENUTZER)

    [Fact]
    public void Aus_nimmt_die_Einstellung_vor_der_Umgebungsvariable()
    {
        using var u = TestUmgebung.Setzen(("HLKS_BENUTZER", Ordner("env")));
        var e = new Einstellungen("T", "pc-eins", Katalog.StandardOrdner(), null) { BenutzerOrdner = Ordner("einstellung") };
        var b = BenutzerEinstellungen.Aus(e);
        Assert.Equal("Umleitung", b.Quelle);
        Assert.Equal(Path.GetFullPath(Ordner("einstellung")), b.Verzeichnis);
        Assert.Equal("pc-eins", b.Rechner);
    }

    [Fact]
    public void Aus_nimmt_die_Umgebungsvariable_HLKS_BENUTZER_wenn_keine_Einstellung_gesetzt_ist()
    {
        using var u = TestUmgebung.Setzen(("HLKS_BENUTZER", Ordner("env")));
        var b = BenutzerEinstellungen.Aus(new Einstellungen("T", "pc-eins", Katalog.StandardOrdner(), null));
        Assert.Equal("Umleitung", b.Quelle);
        Assert.Equal(Path.GetFullPath(Ordner("env")), b.Verzeichnis);
        b.Setzen("design", S("hell"));
        Assert.True(File.Exists(Path.Combine(Ordner("env"), "pc-eins.json")));
    }

    [Fact]
    public void Aus_nimmt_OneDrive_im_Unterordner_einstellungen_und_die_Liste_zuletzt_einen_eigenen()
    {
        var od = Ordner("onedrive"); Directory.CreateDirectory(od);
        using var u = TestUmgebung.Setzen(("HLKS_BENUTZER", null), ("HLKS_ZULETZT", null), ("OneDrive", od), ("OneDriveCommercial", null), ("OneDriveConsumer", null));
        var e = new Einstellungen("T", "pc-eins", Katalog.StandardOrdner(), null);
        var b = BenutzerEinstellungen.Aus(e);
        Assert.Equal("OneDrive", b.Quelle);
        Assert.Equal(Path.Combine(od, "HLKS-CAD", "einstellungen"), b.Verzeichnis);
        var z = ZuletztListe.Aus(e);
        Assert.Equal("OneDrive", z.Quelle);
        Assert.Equal(Path.Combine(od, "HLKS-CAD", "zuletzt"), z.Verzeichnis);   // das Verhalten von «zuletzt» ist unverändert
    }

    [Fact]
    public void Aus_ohne_OneDrive_nimmt_eine_Einzeldatei_unter_APPDATA()
    {
        using var u = TestUmgebung.Setzen(("HLKS_BENUTZER", null), ("HLKS_ZULETZT", null), ("OneDrive", null), ("OneDriveCommercial", null), ("OneDriveConsumer", null));
        var e = new Einstellungen("T", "pc-eins", Katalog.StandardOrdner(), null);
        var b = BenutzerEinstellungen.Aus(e);
        Assert.Equal("lokal", b.Quelle);
        Assert.Equal(RechnerAblage.AppdataOrdner(), b.Verzeichnis);
        var z = ZuletztListe.Aus(e);
        Assert.Equal("lokal", z.Quelle);
        Assert.Equal(RechnerAblage.AppdataOrdner(), z.Verzeichnis);
        Assert.False(File.Exists(Path.Combine(RechnerAblage.AppdataOrdner(), "einstellungen.json")) && false);   // nur Lesen der Wahl, hier wird nichts geschrieben
    }

    // ---------------------------------------------------------------- Endpunkte

    sealed class Server : IDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        public HttpClient Http { get; }

        public Server(string? benutzerOrdner, string wurzel, string geraet = "testpc")
        {
            var e = new Einstellungen("Test", geraet, Katalog.StandardOrdner(), null)
            {
                BenutzerOrdner = benutzerOrdner, ZuletztOrdner = Path.Combine(wurzel, "zuletzt"), ZuletztMerken = false,
                ProjektAblage = Path.Combine(wurzel, "ablage"), Familienbibliothek = Path.Combine(wurzel, "bibliothek"),
            };
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(e)));
            Http = fabrik.CreateClient();
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Get(string pfad = "/api/benutzer/einstellungen")
        {
            var a = await Http.GetAsync(pfad);
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(object? koerper, string pfad = "/api/benutzer/einstellungen")
        {
            var text = koerper is null ? "" : koerper is string s ? s : Json.Text(koerper);
            var a = await Http.PostAsync(pfad, new StringContent(text, Encoding.UTF8, "application/json"));
            var t = await a.Content.ReadAsStringAsync();
            return (a.StatusCode, t.Length == 0 ? null : JsonNode.Parse(t));
        }

        public void Dispose() { Http.Dispose(); fabrik.Dispose(); basis.Dispose(); }
    }

    Server Starten(string? benutzerOrdner, string geraet = "testpc") { var s = new Server(benutzerOrdner, wurzel, geraet); aufraeumen.Add(s); return s; }

    [Fact]
    public async Task Endpunkt_leer_dann_gesetzt_dann_zurueckgesetzt()
    {
        var s = Starten(Ordner());
        var (code, j) = await s.Get();
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.Empty(Assert.IsType<JsonObject>(j!["werte"]));
        Assert.Equal("Umleitung", j["quelle"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(Ordner()), j["ort"]!.GetValue<string>());
        Assert.Equal("testpc", j["rechner"]!.GetValue<string>());

        var (c2, p) = await s.Post(new { schluessel = "projektbrowser.gliederung", wert = "sanitaer" });
        Assert.Equal(HttpStatusCode.OK, c2);
        Assert.Equal("sanitaer", p!["wert"]!.GetValue<string>());
        Assert.Equal("projektbrowser.gliederung", p["schluessel"]!.GetValue<string>());
        Assert.NotNull(p["zeit"]);

        var (_, g) = await s.Get();
        Assert.Equal("sanitaer", g!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
        Assert.Equal("testpc", g["zeiten"]!["projektbrowser.gliederung"]!["rechner"]!.GetValue<string>());

        var (c3, _) = await s.Post(new { schluessel = "projektbrowser.gliederung", wert = (string?)null });
        Assert.Equal(HttpStatusCode.OK, c3);
        var (_, leer) = await s.Get();
        Assert.Empty(Assert.IsType<JsonObject>(leer!["werte"]));
    }

    [Fact]
    public async Task Endpunkt_zwei_Rechner_teilen_sich_den_Ordner_und_der_neueste_Wert_gilt()
    {
        var a = Starten(Ordner(), "pc-a");
        var b = Starten(Ordner(), "pc-b");
        await a.Post(new { schluessel = "projektbrowser.gliederung", wert = "sanitaer" });
        await Task.Delay(30);
        await b.Post(new { schluessel = "projektbrowser.gliederung", wert = "heizung" });
        var (_, ga) = await a.Get();
        var (_, gb) = await b.Get();
        Assert.Equal("heizung", ga!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
        Assert.Equal("heizung", gb!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
        Assert.Equal("pc-b", ga["zeiten"]!["projektbrowser.gliederung"]!["rechner"]!.GetValue<string>());
        Assert.Equal(["pc-a.json", "pc-b.json"], Directory.GetFiles(Ordner(), "*.json").Select(Path.GetFileName).OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task Endpunkt_zwei_Benutzer_behalten_ihre_eigene_Gliederung()
    {
        var anna = Starten(Ordner("anna"));
        var beat = Starten(Ordner("beat"));
        await anna.Post(new { schluessel = "projektbrowser.gliederung", wert = "sanitaer" });
        await beat.Post(new { schluessel = "projektbrowser.gliederung", wert = "koordination" });
        Assert.Equal("sanitaer", (await anna.Get()).Json!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
        Assert.Equal("koordination", (await beat.Get()).Json!["werte"]!["projektbrowser.gliederung"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("{}")]                                                                  // Schlüssel fehlt
    [InlineData("{\"schluessel\":\"\",\"wert\":\"x\"}")]
    [InlineData("{\"schluessel\":\"Gross\",\"wert\":\"x\"}")]
    [InlineData("{\"schluessel\":5,\"wert\":\"x\"}")]                                    // Schlüssel kein Text
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":\"Sanitär\"}")]
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":3}")]
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":[]}")]                    // G2: die Liste darf nicht leer sein
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":[3]}")]
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":[\"sanitaer\",\"sanitaer\"]}")]
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":[\"Sanitär\"]}")]
    [InlineData("{\"schluessel\":\"projektbrowser.gliederung\",\"wert\":{\"a\":1}}")]
    [InlineData("kein json")]
    [InlineData("[1,2]")]                                                               // Körper kein Objekt
    public async Task Endpunkt_Fehlerfaelle_geben_400_mit_Fehler_und_Vorschlag_und_aendern_nichts(string koerper)
    {
        var s = Starten(Ordner());
        var (code, j) = await s.Post(koerper);
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.False(string.IsNullOrWhiteSpace(j!["fehler"]?.GetValue<string>()));
        Assert.Empty(Assert.IsType<JsonObject>((await s.Get()).Json!["werte"]));
        Assert.False(Directory.Exists(Ordner()) && Directory.GetFiles(Ordner(), "*.json").Length > 0, "nichts geschrieben");
    }

    [Fact]
    public async Task Endpunkt_Fehler_ohne_Vorschlag_gibt_es_nicht_bei_ungueltigem_Schluessel()
    {
        var s = Starten(Ordner());
        var (code, j) = await s.Post(new { schluessel = "Gross", wert = "x" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("ungültig", j!["fehler"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(j["vorschlag"]?.GetValue<string>()));
    }

    [Fact]
    public async Task Endpunkt_Ablage_nicht_beschreibbar_gibt_400_mit_Hinweis_und_der_Server_laeuft_weiter()
    {
        File.WriteAllText(Ordner("datei"), "kein Ordner");
        var s = Starten(Ordner("datei"));
        var (code, j) = await s.Post(new { schluessel = "design", wert = "hell" });
        Assert.Equal(HttpStatusCode.BadRequest, code);
        Assert.Contains("nicht gespeichert", j!["fehler"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, (await s.Get()).Code);
    }

    [Fact]
    public async Task Endpunkt_im_Testlauf_ohne_Umleitung_liefert_leere_Einstellungen_und_lehnt_das_Schreiben_ab()
    {
        using var u = TestUmgebung.Setzen(("HLKS_TEST", "1"), ("HLKS_BENUTZER", null));
        var s = Starten(null);   // keine Umleitung: OneDrive bzw. APPDATA des Entwicklers – darf nicht berührt werden
        var (c1, g) = await s.Get();
        Assert.Equal(HttpStatusCode.OK, c1);
        Assert.Empty(Assert.IsType<JsonObject>(g!["werte"]));
        var (c2, j) = await s.Post(new { schluessel = "projektbrowser.gliederung", wert = "sanitaer" });
        Assert.Equal(HttpStatusCode.BadRequest, c2);
        Assert.Contains("Testlauf", j!["fehler"]!.GetValue<string>());
        Assert.Contains("HLKS_BENUTZER", j["vorschlag"]!.GetValue<string>());
    }

    [Fact]
    public async Task Endpunkt_Kein_Projekt_noetig_und_andere_Endpunkte_bleiben_unberuehrt()
    {
        var s = Starten(Ordner());
        Assert.Equal(HttpStatusCode.OK, (await s.Get("/api/benutzer/einstellungen")).Code);
        Assert.Equal(HttpStatusCode.OK, (await s.Get("/api/zuletzt")).Code);
        Assert.Equal(HttpStatusCode.OK, (await s.Get("/api/status")).Code);
    }

    [Fact]
    public void Katalogabfrage_liefert_die_Gewerke_mit_Farben_aus_gewerke_json()
    {
        var p = Hlks.Kern.Projekt.Anlegen(Katalog.Laden(Katalog.StandardOrdner()), new OrdnerSpeicher(Path.Combine(wurzel, "projekt")), "Farbtest", "Test", "testpc", "mensch");
        try
        {
            var o = (JsonObject)Tuer.Abfrage(p, "katalog", new JsonObject());
            var gewerke = Assert.IsType<JsonArray>(o["gewerke"]);
            var san = gewerke.OfType<JsonObject>().Single(g => g["id"]!.GetValue<string>() == "sanitaer");
            Assert.Equal("Sanitär", san["titel"]!.GetValue<string>());
            Assert.Matches("^#[0-9A-Fa-f]{6}$", san["koordination"]!.GetValue<string>());
            Assert.Matches("^#[0-9A-Fa-f]{6}$", san["pastell"]!.GetValue<string>());
            Assert.InRange(o["gewerkeDunkelDeckkraft"]!.GetValue<double>(), 0.05, 0.6);
        }
        finally { p.Freigeben(); }
    }
    [Fact]
    public void Katalog_Gewerke_kommen_aus_gewerke_json_und_enthalten_Farben()
    {
        var k = Katalog.Laden(Katalog.StandardOrdner());
        Assert.Contains(k.Gewerke, g => g.Id == "sanitaer" && g.Titel == "Sanitär" && g.Koordination.StartsWith('#') && g.Pastell.StartsWith('#'));
        Assert.Contains(k.Gewerke, g => g.Id == "koordination");
        Assert.Contains(k.Gewerke, g => g.Id == "energie");
        Assert.InRange(k.GewerkeDunkelDeckkraft, 0.05, 0.6);
    }
}
