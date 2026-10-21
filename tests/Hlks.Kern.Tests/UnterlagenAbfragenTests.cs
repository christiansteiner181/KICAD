using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Testleser mit einstellbarer Version: prüft, dass eine neue Leserversion neu liest und die Ablage sonst genügt.</summary>
public sealed class TestLeserB2 : IUnterlagenLeser
{
    public static int Version_ = 1;
    public static int Gelesen;
    public static bool Wirft;
    public string Name => "testleserb2";
    public int Version => Version_;
    public IReadOnlyCollection<string> Arten { get; } = ["testartb2"];
    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        Interlocked.Increment(ref Gelesen);
        if (Wirft) throw new InvalidOperationException("Leser kaputt");
        var e = new UnterlagenBefunde { Datei = datei, Pruefsumme = pruefsumme, Art = art, Leser = $"{Name} {Version}" };
        e.Befunde.Add(new UnterlagenBefund { Id = $"{pruefsumme[..12]}#1", Datei = datei, Art = "text", Wert = "hallo", Leser = e.Leser });
        return e;
    }
}

/// <summary>Ablage, unterlage_einfuegen mit zweck «familie» und die Abfragen unterlagen_lesen, befunde und unterlage_bild (Modul 2, B2).</summary>
public class UnterlagenAbfragenTests
{
    static JsonObject Abf(Projekt p, string name, JsonObject par) => (JsonObject)Json.Knoten(Tuer.Abfrage(p, name, par) is JsonNode n ? n : throw new Xunit.Sdk.XunitException("keine JSON-Antwort"));

    static JsonObject Lesen(Projekt p, params string[] bezug) => Abf(p, "unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray(bezug.Select(b => (JsonNode)b).ToArray()) });

    // ---------- Ablage ----------

    [Fact]
    public void Wiederholtes_Lesen_kommt_aus_der_Ablage_unter_einer_Sekunde()
    {
        using var o = new Ordner();
        var p = Leer();
        var pdf = TestPdf(o);
        var u = UnterlagenService.Aufloesen(p, pdf);
        Assert.Equal("pdf", u.Art);
        var erst = UnterlagenService.Lesen(p, u);
        Assert.False(erst.AusAblage);
        var datei = Path.Combine(Unterlagen.Ordner(p), "befunde", $"{u.Pruefsumme}.pdf-3.json");
        Assert.Equal(datei, erst.AblageDatei);
        Assert.True(File.Exists(datei));
        var zweit = UnterlagenService.Lesen(p, u);
        Assert.True(zweit.AusAblage);
        Assert.True(zweit.Sekunden < 1, $"aus der Ablage {zweit.Sekunden:0.00} s");
        Assert.Equal(erst.Befunde.Befunde.Count, zweit.Befunde.Befunde.Count);
        // auch von der Platte, wenn der Speicher leer ist (zweiter Prozess): Datei lesen
        var neuLesen = UnterlagenService.Laden(datei)!;
        Assert.Equal(erst.Befunde.Befunde.Select(x => x.Id), neuLesen.Befunde.Select(x => x.Id));
        Assert.Equal(erst.Befunde.Befunde.Count(x => x.Art == "zelle"), neuLesen.Befunde.Count(x => x.Art == "zelle"));
    }

    [Fact]
    public void Neue_Leserversion_liest_neu_die_alte_Datei_bleibt()
    {
        using var o = new Ordner();
        var p = Leer();
        var pfad = Datei(o, "x.bin", "Inhalt");
        var (name, stand) = Unterlagen.Ablegen(p, pfad);
        var u = new UnterlageRef(name, "x.bin", "testartb2", stand, Path.Combine(Unterlagen.Ordner(p), name), null, null);
        TestLeserB2.Version_ = 1;
        int vorher = TestLeserB2.Gelesen;
        var a = UnterlagenService.Lesen(p, u);
        Assert.False(a.AusAblage);
        Assert.True(UnterlagenService.Lesen(p, u).AusAblage);
        Assert.Equal(vorher + 1, TestLeserB2.Gelesen);
        TestLeserB2.Version_ = 2;
        try
        {
            var b = UnterlagenService.Lesen(p, u);
            Assert.False(b.AusAblage);
            Assert.Equal(vorher + 2, TestLeserB2.Gelesen);
            Assert.Equal("testleserb2 2", b.Befunde.Leser);
            Assert.True(File.Exists(Path.Combine(Unterlagen.Ordner(p), "befunde", $"{stand}.testleserb2-1.json")));
            Assert.True(File.Exists(Path.Combine(Unterlagen.Ordner(p), "befunde", $"{stand}.testleserb2-2.json")));
        }
        finally { TestLeserB2.Version_ = 1; }
    }

    [Fact]
    public void Unterlage_ueber_Kennzeichen_Id_Dateiname_und_Pfad()
    {
        using var o = new Ordner();
        var p = Leer();
        var pdf = TestPdf(o, "daten.pdf");
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = pdf, ["zweck"] = "familie" }));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        foreach (var bezug in new[] { ul.Kennzeichen, ul.Id, "daten.pdf", ul.Text("datei")! })
        {
            var u = UnterlagenService.Aufloesen(p, bezug);
            Assert.Equal("pdf", u.Art);
            Assert.Equal(ul.Kennzeichen, u.Kennzeichen);
        }
        var ex = Assert.Throws<HlksFehler>(() => UnterlagenService.Aufloesen(p, "gibtsnicht.pdf"));
        Assert.Contains("UL-1", ex.Vorschlag);
        // ein Dateipfad ausserhalb des Ordners wird abgelegt
        using var o2 = new Ordner();
        var u2 = UnterlagenService.Aufloesen(p, TestPdf(o2, "andere.pdf"));
        Assert.Equal("andere.pdf", u2.Original);
        Assert.True(File.Exists(Path.Combine(Unterlagen.Ordner(p), u2.Datei)));
    }

    // ---------- unterlage_einfuegen zweck familie ----------

    [Fact]
    public void Familienunterlage_hat_weder_Geschoss_noch_Lage_noch_Geometrie()
    {
        using var o = new Ordner();
        var p = MitGeschoss();
        var e = Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestPdf(o), ["zweck"] = "familie" }));
        Assert.Contains(e.Hinweise, h => h.Contains("ohne Geschoss und Lage"));
        var ul = Unterlage(p);
        Assert.Null(ul.Geschoss);
        Assert.Null(ul.Geometrie);
        Assert.Null(ul.Eigenschaft("transformation"));
        Assert.Equal("familie", ul.Text("zweck"));
        Assert.Equal("pdf", ul.Text("art"));
        Assert.False(Grafik.Bool(ul, "anzeigen", true));
        Assert.Equal(1, p.Modell.DerKlasse("Unterlage").Count());
    }

    [Fact]
    public void Familienunterlage_wird_in_Plaenen_nicht_gezeichnet()
    {
        using var o = new Ordner();
        var p = MitGeschoss();
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestPdf(o), ["zweck"] = "familie" }));
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestDxf(o), ["zweck"] = "familie" }));
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = StlBinaer(o), ["zweck"] = "familie" }));
        // Grundlagen-Grafik und volle Plangrafik: nichts von den Herstellerunterlagen
        Assert.Empty(Grundlage(PlanGrundlage(p)));
        var plan = Grafik.Plan(p, "AN-EG");
        Assert.DoesNotContain(plan.Teile, t => t.Ebene == "grundlage");
        Assert.DoesNotContain(plan.Teile.SelectMany(t => t.Primitive), pr => pr.Art == "bild");
        // und das Modell bleibt prüfbar
        Assert.NotNull(new Abfragen(p).Pruefen(null));
    }

    [Fact]
    public void Neue_Arten_nur_mit_zweck_familie()
    {
        using var o = new Ordner();
        var p = MitGeschoss();
        var dateien = new (string Pfad, string Art)[]
        {
            (StlBinaer(o), "stl"), (Obj(o), "obj"), (Datei(o, "t.csv", CsvText), "csv"), (TestXlsx(o), "xlsx"),
            (Datei(o, "k.txt", TypkatalogText), "typkatalog"), (Datei(o, "m.step", "ISO-10303-21;\nEND-ISO-10303-21;\n"), "step"),
            (Datei(o, "m.igs", "iges"), "iges"), (TestIfc(o), "ifc"), (TestDxf(o), "dxf"),
        };
        foreach (var (pfad, art) in dateien)
        {
            var par = new JsonObject { ["datei"] = pfad, ["zweck"] = "familie" };
            if (art == "typkatalog") par["art"] = "typkatalog";   // .txt allein sagt es nicht
            Ok(p.Ausfuehren("unterlage_einfuegen", par));
            Assert.Contains(p.Modell.DerKlasse("Unterlage"), u => u.Text("art") == art);
        }
        // ohne zweck: Herstellerarten sind abgelehnt, mit Vorschlag
        var nein = Nein(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = StlBinaer(o, "zwei.stl"), ["geschoss"] = "EG" }));
        Assert.Contains("zweck", nein.Vorschlag!);
        Nein(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestIfc(o, "zwei.ifc"), ["geschoss"] = "EG" }));
        // ohne Geschoss und ohne zweck: wie bisher «geschoss fehlt»
        var fehlt = Nein(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestDxf(o, "drei.dxf") }));
        Assert.Contains("geschoss", fehlt.Fehler!);
        Nein(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestDxf(o, "vier.dxf"), ["zweck"] = "irgendwas" }));
    }

    [Fact]
    public void Dieselbe_Datei_erneut_aendert_nichts_und_Kalibrieren_wird_abgelehnt()
    {
        using var o = new Ordner();
        var p = MitGeschoss();
        var pfad = TestPdf(o);
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = pfad, ["zweck"] = "familie" }));
        var e = Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = pfad, ["zweck"] = "familie" }));
        Assert.Contains(e.Hinweise, h => h.Contains("unverändert"));
        Assert.Equal(1, p.Modell.DerKlasse("Unterlage").Count());
        var k = Nein(Kalibrieren(p, "UL-1", [[0, 0], [100, 0]], [[0, 0], [1000, 0]]));
        Assert.Contains("Herstellerunterlage", k.Fehler!);
    }

    [Fact]
    public void Bezug_zur_Familie_wird_geprueft()
    {
        using var o = new Ordner();
        var p = MitGeschoss();
        var nein = Nein(p.Ausfuehren("unterlage_einfuegen", new JsonObject
        {
            ["datei"] = TestPdf(o), ["zweck"] = "familie", ["bezuege"] = new JsonObject { ["familie"] = "FAM-99" },
        }));
        Assert.Contains("FAM-99", nein.Fehler!);
        Nein(p.Ausfuehren("unterlage_einfuegen", new JsonObject
        {
            ["datei"] = TestPdf(o), ["zweck"] = "familie", ["bezuege"] = new JsonObject { ["wand"] = "W-1" },
        }));
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
    }

    // ---------- unterlagen_lesen ----------

    [Fact]
    public void Unterlagen_lesen_gibt_eine_kompakte_Zusammenfassung()
    {
        using var o = new Ordner();
        var p = Leer();
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestPdf(o), ["zweck"] = "familie" }));
        var a = Lesen(p, "UL-1");
        var u = (JsonObject)a["unterlagen"]![0]!;
        Assert.Equal("pdf", u["art"]!.GetValue<string>());
        Assert.Equal(2, u["seiten"]!.GetValue<int>());
        Assert.Equal("pdf 3", u["leser"]!.GetValue<string>());
        Assert.False(u["ausAblage"]!.GetValue<bool>());
        Assert.True(u["jeArt"]!["wort"]!.GetValue<int>() > 20);
        Assert.Equal(2, u["jeArt"]!["tabelle"]!.GetValue<int>());
        var tabellen = (JsonArray)u["tabellen"]!;
        Assert.Equal(2, tabellen.Count);
        Assert.Equal(["Typ", "DN [mm]", "L [mm]", "H [mm]"], ((JsonArray)tabellen[0]!["kopf"]!).Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(3, tabellen[0]!["zeilen"]!.GetValue<int>());
        Assert.Equal(2, ((JsonArray)u["seitenUebersicht"]!).Count);
        Assert.NotNull(u["hinweise"]);
        // nie alle Wörter: die Antwort enthält keine Wortliste und bleibt klein
        var text = a.ToJsonString();
        Assert.DoesNotContain("\"Testventil\"", text.Replace("Datenblatt Testventil", ""));
        Assert.True(text.Length < 15_000, $"Antwort {text.Length} Zeichen");
        // zweites Mal aus der Ablage
        Assert.True(((JsonObject)Lesen(p, "UL-1")["unterlagen"]![0]!)["ausAblage"]!.GetValue<bool>());
    }

    [Fact]
    public void Unterlagen_lesen_mehrere_Arten_ein_Fehler_bricht_nicht_alles_ab()
    {
        using var o = new Ordner();
        var p = Leer();
        var a = Lesen(p, TestDxf(o), Datei(o, "t.csv", CsvText), StlBinaer(o), TestIfc(o), "gibtsnicht.pdf", Datei(o, "bild.tif", "x"));
        var us = ((JsonArray)a["unterlagen"]!).Cast<JsonObject>().ToList();
        Assert.Equal(["dxf", "csv", "stl", "ifc"], us.Take(4).Select(x => x["art"]!.GetValue<string>()).ToArray());
        var dxf = us[0];
        Assert.Equal(1, dxf["jeArt"]!["block"]!.GetValue<int>());
        Assert.Equal(2, dxf["jeArt"]!["mass"]!.GetValue<int>());
        Assert.Equal(67, dxf["masse"]![0]!["wert"]!.GetValue<double>(), 1e-9);
        Assert.Equal(new[] { "BLOECKE", "GEHAEUSE", "KONTUR", "MASSE", "TEXT" }, dxf["layer"]!.AsObject().Select(k => k.Key).OrderBy(x => x).ToArray());
        Assert.Equal(1, us[1]["tabellen"]!.AsArray().Count);
        Assert.Equal(12, us[2]["form3d"]![0]!["anzahlDreiecke"]!.GetValue<int>());
        Assert.Equal(2, us[3]["ports"]!.AsArray().Count);
        Assert.Equal("V-15 Kugelhahn", us[3]["typen"]![0]!["name"]!.GetValue<string>());
        Assert.NotNull(us[4]["fehler"]);      // gibt es nicht
        Assert.NotNull(us[4]["vorschlag"]);
        Assert.NotNull(us[5]["fehler"]);      // TIFF: keine Art
        // N5: der Aufruf meldet die nicht gelesenen oben (Palette, Auftragsprotokoll und MCP zeigen ihn als abgelehnt), die gelesenen bleiben
        Assert.StartsWith("2 von 6 Unterlagen nicht gelesen: «gibtsnicht.pdf»: ", a["fehler"]!.GetValue<string>());
        Assert.Equal(us[4]["vorschlag"]!.GetValue<string>(), a["vorschlag"]!.GetValue<string>());
        Assert.Equal(4, a["gelesen"]!.GetValue<int>());
        Assert.True(Werkzeugergebnis.MeldetFehler(a.ToJsonString()));
    }

    [Fact]
    public void Unterlagen_lesen_N5_eine_abgelehnte_Unterlage_Fehler_oben_ohne_Zaehlung_gelesene_ohne_Fehler()
    {
        using var o = new Ordner();
        var p = Leer();
        var nein = Lesen(p, "gibtsnicht.pdf");
        Assert.Equal(nein["unterlagen"]![0]!["fehler"]!.GetValue<string>(), nein["fehler"]!.GetValue<string>());
        Assert.Null(nein["gelesen"]);
        Assert.True(Werkzeugergebnis.MeldetFehler(nein.ToJsonString()));
        var ja = Lesen(p, Datei(o, "t.csv", CsvText));
        Assert.Null(ja["fehler"]);
        Assert.False(Werkzeugergebnis.MeldetFehler(ja.ToJsonString()));
    }

    // ---------- befunde ----------

    [Fact]
    public void Befunde_filtern_nach_Seite_Art_Text_und_Box()
    {
        using var o = new Ordner();
        var p = Leer();
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestPdf(o), ["zweck"] = "familie" }));
        // Art + Text
        var z = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["art"] = "zelle", ["text"] = "67" });
        Assert.Equal(1, z["treffer"]!.GetValue<int>());
        var zb = (JsonObject)z["befunde"]![0]!;
        Assert.Equal(67, zb["wert"]!.GetValue<double>());
        Assert.Equal("mm", zb["einheit"]!.GetValue<string>());
        Assert.Equal(1, zb["seite"]!.GetValue<int>());
        Assert.Null(zb["sicherheit"]);      // 1 wird weggelassen
        // Seite
        var s2 = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["art"] = "tabelle", ["seite"] = 2 });
        Assert.Equal(1, s2["treffer"]!.GetValue<int>());
        // Box: der Titel liegt oben links
        var box = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["art"] = "wort", ["seite"] = 1, ["box"] = new JsonArray(40, 775, 200, 800) });
        Assert.Equal(["Datenblatt", "Testventil"], box["befunde"]!.AsArray().Select(x => x!["wert"]!.GetValue<string>()).ToArray());
        // mehrere Arten, Id
        var id = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["id"] = "1, 2" });
        Assert.Equal(["seite", "wort"], id["befunde"]!.AsArray().Select(x => x!["art"]!.GetValue<string>()).ToArray());
        Assert.Equal(2, id["treffer"]!.GetValue<int>());
        // keine Treffer: Hinweis mit den vorhandenen Arten
        var leer = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["text"] = "gibtsnirgends" });
        Assert.Equal(0, leer["treffer"]!.GetValue<int>());
        Assert.Contains("wort", leer["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public void Befunde_seitenweise_mit_weiter()
    {
        using var o = new Ordner();
        var p = Leer();
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestPdf(o), ["zweck"] = "familie" }));
        var alle = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["art"] = "wort", ["proSeite"] = 10 });
        int gesamt = alle["treffer"]!.GetValue<int>();
        Assert.True(gesamt > 30);
        Assert.Equal(10, alle["befunde"]!.AsArray().Count);
        Assert.Equal((int)Math.Ceiling(gesamt / 10.0), alle["trefferSeiten"]!.GetValue<int>());
        Assert.Equal(2, alle["weiter"]!["trefferSeite"]!.GetValue<int>());
        var ids = new HashSet<string>();
        for (int s = 1; s <= alle["trefferSeiten"]!.GetValue<int>(); s++)
        {
            var seite = Abf(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["art"] = "wort", ["proSeite"] = 10, ["trefferSeite"] = s });
            foreach (var b in seite["befunde"]!.AsArray()) Assert.True(ids.Add(b!["id"]!.GetValue<string>()));
            if (s == alle["trefferSeiten"]!.GetValue<int>()) Assert.Null(seite["weiter"]);
        }
        Assert.Equal(gesamt, ids.Count);
    }

    [Fact]
    public void Befunde_3D_Netz_nur_mit_voll()
    {
        using var o = new Ordner();
        var p = Leer();
        var kurz = Abf(p, "befunde", new JsonObject { ["unterlage"] = StlBinaer(o), ["art"] = "form3d" });
        var w = (JsonObject)kurz["befunde"]![0]!["wert"]!;
        Assert.Null(w["ecken"]);
        Assert.Equal(12, w["anzahlDreiecke"]!.GetValue<int>());
        Assert.NotNull(w["huelle"]);
        var voll = Abf(p, "befunde", new JsonObject { ["unterlage"] = "quader.stl", ["art"] = "form3d", ["voll"] = true });
        Assert.Equal(8, voll["befunde"]![0]!["wert"]!["ecken"]!.AsArray().Count);
    }

    [Fact]
    public void Befunde_Antwort_bleibt_unter_der_Zeichengrenze()
    {
        using var o = new Ordner();
        var p = Leer();
        var a = Abf(p, "befunde", new JsonObject { ["unterlage"] = TestPdf(o), ["proSeite"] = 500 });
        Assert.True(a.ToJsonString().Length < UnterlagenAbfrage.MaxZeichen + 5_000);
    }

    // ---------- unterlage_bild ----------

    static (int Breite, int Hoehe, byte[] Png) Bild(JsonObject a)
    {
        var png = Convert.FromBase64String(a["base64"]!.GetValue<string>());
        Assert.Equal([0x89, (byte)'P', (byte)'N', (byte)'G'], png[..4]);
        int b = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19], h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        Assert.Equal(a["breite"]!.GetValue<int>(), b);
        Assert.Equal(a["hoehe"]!.GetValue<int>(), h);
        return (b, h, png);
    }

    [Fact]
    public void Unterlage_bild_PDF_Seite_und_Ausschnitt()
    {
        using var o = new Ordner();
        var p = Leer();
        Ok(p.Ausfuehren("unterlage_einfuegen", new JsonObject { ["datei"] = TestPdf(o), ["zweck"] = "familie" }));
        var ganz = Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "UL-1" });
        var (b, h, _) = Bild(ganz);
        Assert.InRange(b, 995, 1005);
        Assert.InRange(h, 1400, 1425);   // A4-Seitenverhältnis
        Assert.Equal("image/png", ganz["mime"]!.GetValue<string>());
        Assert.Equal(2, ganz["seiten"]!.GetValue<int>());
        Assert.Equal([0, 0, 595, 842], ganz["bereich"]!.AsArray().Select(x => Math.Round(x!.GetValue<double>())).ToArray());
        // Ausschnitt: die Tabelle der Seite 1 (pt, y nach oben), breite 600
        var teil = Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "UL-1", ["seite"] = 1, ["ausschnitt"] = new JsonArray(40, 575, 350, 655), ["breite"] = 600 });
        var (tb, th, _) = Bild(teil);
        Assert.InRange(tb, 595, 605);
        Assert.InRange(th, 150, 160);   // 80 pt hoch bei 600 / 310 Bildpunkten je pt
        var bereich = teil["bereich"]!.AsArray().Select(x => x!.GetValue<double>()).ToArray();
        Assert.InRange(bereich[0], 39, 41); Assert.InRange(bereich[1], 574, 576); Assert.InRange(bereich[2], 349, 351); Assert.InRange(bereich[3], 654, 656);
        Assert.Contains("y nach oben", teil["koordinaten"]!.GetValue<string>());
        // Seite 2
        Bild(Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "UL-1", ["seite"] = 2, ["breite"] = 300 }));
        // Fehler
        Assert.Throws<HlksFehler>(() => Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "UL-1", ["seite"] = 3 }));
        Assert.Throws<HlksFehler>(() => Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "UL-1", ["ausschnitt"] = new JsonArray(1000, 1000, 1100, 1100) }));
    }

    [Fact]
    public void Unterlage_bild_Ausschnitt_zeigt_wirklich_den_Ausschnitt()
    {
        // Seite 1 enthält oben ein schwarzes Rechteck-Paar; der Ausschnitt um das Rechteck (100/520, 200 × 80 von oben) ist deutlich dunkler als leerer Rand
        using var o = new Ordner();
        var p = Leer();
        var pdf = TestPdf(o);
        var rand = Bild(Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = pdf, ["ausschnitt"] = new JsonArray(400, 20, 560, 100), ["breite"] = 320 }));
        var kontur = Bild(Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "datenblatt.pdf", ["ausschnitt"] = new JsonArray(90, 842 - 610, 310, 842 - 510), ["breite"] = 440 }));
        Assert.True(Dunkel(kontur.Png) > Dunkel(rand.Png) + 0.005, $"Kontur {Dunkel(kontur.Png):0.0000} gegen Rand {Dunkel(rand.Png):0.0000}");
    }

    /// <summary>Anteil dunkler Bildpunkte (Mittel der Rot-Grün-Blau-Werte unter 128) im PNG.</summary>
    static double Dunkel(byte[] png)
    {
        using var datei = new Ordner();
        var pfad = datei.Datei("t.png"); File.WriteAllBytes(pfad, png);
        var (b, h, rgb) = PngLesen(pfad);
        int dunkel = 0;
        for (int i = 0; i < b * h; i++) if ((rgb[3 * i] + rgb[3 * i + 1] + rgb[3 * i + 2]) / 3 < 128) dunkel++;
        return dunkel / (double)(b * h);
    }

    [Fact]
    public void Unterlage_bild_DXF_wird_gezeichnet_Bild_direkt_und_3D_hat_keins()
    {
        using var o = new Ordner();
        var p = Leer();
        var dxf = Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = TestDxf(o), ["breite"] = 800 });
        var (b, h, png) = Bild(dxf);
        Assert.InRange(b, 790, 810);
        Assert.Contains("Gezeichnet", string.Join(" ", dxf["hinweise"]!.AsArray().Select(x => x!.GetValue<string>())));
        Assert.True(Dunkel(png) > 0.0005, "die Zeichnung hat dunkle Linien");
        // Bildunterlage direkt (PNG kleiner als breite: unverändert), mit Ausschnitt zugeschnitten
        var png1 = Png(o, "klein.png", 200, 100, (10, 120, 200));
        var direkt = Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = png1 });
        Assert.Equal(200, direkt["breite"]!.GetValue<int>());
        var teil = Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = "klein.png", ["ausschnitt"] = new JsonArray(50, 20, 150, 70) });
        Assert.Equal(100, teil["breite"]!.GetValue<int>());
        Assert.Equal(50, teil["hoehe"]!.GetValue<int>());
        // STL: kein Bild, mit Vorschlag
        var ex = Assert.Throws<HlksFehler>(() => Abf(p, "unterlage_bild", new JsonObject { ["unterlage"] = StlBinaer(o) }));
        Assert.Contains("form3d", ex.Vorschlag);
    }

    // ---------- Definitionen ----------

    [Fact]
    public void Abfragen_stehen_in_Faehigkeiten()
    {
        var p = Leer();
        var f = new Abfragen(p).Faehigkeiten();
        var namen = ((JsonArray)f["abfragen"]!).Select(x => x!["name"]!.GetValue<string>()).ToList();
        Assert.Contains("unterlagen_lesen", namen);
        Assert.Contains("befunde", namen);
        Assert.Contains("unterlage_bild", namen);
        var e = Json.Knoten(p.Befehle.Hole("unterlage_einfuegen").Parameter).AsArray().Select(x => x!["name"]!.GetValue<string>()).ToList();
        Assert.Contains("zweck", e);
        Assert.Contains("bezuege", e);
    }
}

/// <summary>Ein Fehler beim Lesen einer Datei (kaputte STEP-Datei, fehlendes Node, Zeitlimit) bricht das Lesen der übrigen nicht ab.</summary>
public class UnterlagenFehlerJeDateiTests
{
    [Fact]
    public void Kaputte_stp_neben_gueltiger_Datei()
    {
        using var o = new Ordner();
        var p = UnterlagenHilfe.Leer();
        var kaputt = UnterlagenHilfe.Datei(o, "kaputt.stp", "das ist kein STEP");
        var gut = UnterlagenHilfe.StlBinaer(o);
        var a = (JsonObject)Json.Knoten(Tuer.Abfrage(p, "unterlagen_lesen", new JsonObject { ["unterlagen"] = new JsonArray(kaputt, gut, UnterlagenHilfe.Datei(o, "kaputt2.csv", "")) }));
        var us = ((JsonArray)a["unterlagen"]!).Cast<JsonObject>().ToList();
        Assert.Equal(3, us.Count);
        // die STEP-Datei: Fehler dieser Datei (HlksFehler oder gemeldet), kein Abbruch
        Assert.True(us[0]["fehler"] is not null || us[0]["hinweise"] is not null, us[0].ToJsonString());
        Assert.Equal("stl", us[1]["art"]!.GetValue<string>());
        Assert.Equal(12, us[1]["form3d"]![0]!["anzahlDreiecke"]!.GetValue<int>());
        Assert.Equal("csv", us[2]["art"]!.GetValue<string>());
    }

    [Fact]
    public void Leser_der_eine_beliebige_Ausnahme_wirft_wird_je_Datei_gemeldet()
    {
        using var o = new Ordner();
        var p = UnterlagenHilfe.Leer();
        var pfad = UnterlagenHilfe.Datei(o, "boese.bin", "x");
        var (name, _) = Unterlagen.Ablegen(p, pfad);
        TestLeserB2.Wirft = true;
        try
        {
            var u = new UnterlageRef(name, "boese.bin", "testartb2", "123456789012", Path.Combine(Unterlagen.Ordner(p), name), null, null);
            var ex = Assert.Throws<HlksFehler>(() => UnterlagenService.Lesen(p, u));
            Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
        }
        finally { TestLeserB2.Wirft = false; }
    }
}
