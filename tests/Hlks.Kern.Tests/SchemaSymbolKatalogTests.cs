using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5, Teil A-S 2 (zweiter Anlauf): Katalog der Schemasymbole aus Christians HKAP-Symbolbibliothek (kataloge/schemasymbole.json).
/// Quelle der Gegenprobe: tests/daten/hkap/schema_symbole_testkopie_2026-10-14.json, eine Kopie der Testkopie von HKAP (data/schema_symbole.json,
/// Stand 14.10.2026, 155 Einträge), nur zum Vergleich. Der Katalog ist die wirksame Bibliothek (HKAP SchemaSymbole.Alle): eingebaute Symbole,
/// durch gleichnamige Einträge der Datei ersetzt, plus die eigenen; in dieser Datei steht jedes eingebaute Symbol als Eintrag (angepasst oder
/// ausgeblendet), die wirksame Bibliothek ist also genau die Datei.</summary>
public class SchemaSymbolKatalogTests
{
    const string Quelle = "hkap/schema_symbole_testkopie_2026-10-14.json";

    static SchemaSymbolKatalog Sk => Kat.Schemasymbole ?? throw new Xunit.Sdk.XunitException("kataloge/schemasymbole.json fehlt");
    static JsonArray QuelleDaten() => JsonNode.Parse(File.ReadAllText(IfcHilfe.Testdaten(Quelle)))!.AsArray();

    [Fact]
    public void Katalog_Anzahl_Keys_Und_Herkunft_Entsprechen_Der_Quelle()
    {
        var q = QuelleDaten();
        Assert.Equal(155, q.Count);
        Assert.Equal(q.Count, Sk.Symbole.Count);
        Assert.Equal(q.Select(s => s!["key"]!.GetValue<string>()).Order(StringComparer.Ordinal), Sk.Symbole.Select(s => s.Key).Order(StringComparer.Ordinal));   // gleiche Keys (die Reihenfolge ist die der HKAP: eingebaute zuerst, dann eigene)
        Assert.Equal(q.Count(s => s!["versteckt"]!.GetValue<bool>()), Sk.Symbole.Count(s => s.Versteckt));
        Assert.Equal(q.Count(s => !s!["versteckt"]!.GetValue<bool>()), Sk.Sichtbare.Count());
        // Herkunft: eigene Symbole «eig-…», sonst angepasst (eingebaut und in der Datei); nichts mehr «HKAP-Code», weil die Datei alle eingebauten ersetzt
        foreach (var s in Sk.Symbole)
            Assert.Equal(s.Key.StartsWith("eig-") ? "HKAP data/schema_symbole.json (eigen)" : "HKAP data/schema_symbole.json (angepasst)", s.Herkunft);
        var anzahl = Sk.Kopf.Anzahl!;
        Assert.Equal(155, anzahl["symbole"]!.GetValue<int>());
        Assert.Equal(Sk.Symbole.Count(s => s.Herkunft.EndsWith("(eigen)")), anzahl["eigen"]!.GetValue<int>());
        Assert.Equal(Sk.Symbole.Count(s => s.Herkunft.EndsWith("(angepasst)")), anzahl["angepasst"]!.GetValue<int>());
        Assert.Equal(Sk.Symbole.Count(s => s.Versteckt), anzahl["ausgeblendet"]!.GetValue<int>());
    }

    [Fact]
    public void Katalog_Geometrie_Anschluesse_Und_Kopfdaten_Gleich_Der_Quelle()
    {
        var q = QuelleDaten();
        foreach (var qs in q)
        {
            var key = qs!["key"]!.GetValue<string>();
            var s = Sk.Symbol(key) ?? throw new Xunit.Sdk.XunitException(key + " fehlt");
            Assert.Equal(qs["gruppe"]!.GetValue<string>(), s.Gruppe);
            Assert.Equal(qs["sparte"]!.GetValue<string>(), s.Sparte);
            Assert.Equal(qs["label"]!.GetValue<string>(), s.Label);
            Assert.Equal(qs["labelPos"]!.GetValue<string>(), s.LabelPos);
            Assert.Equal(qs["standardFarbe"]!.GetValue<string>(), s.StandardFarbe);
            Assert.Equal(qs["versteckt"]!.GetValue<bool>(), s.Versteckt);
            var gq = qs["geo"]!.AsArray();
            Assert.Equal(gq.Count, s.Geo.Count);
            for (int i = 0; i < gq.Count; i++)
            {
                var a = gq[i]!; var g = s.Geo[i];
                Assert.Equal(a["t"]!.GetValue<string>(), g.T);
                Assert.Equal(a["p"]!.AsArray().Select(v => v!.GetValue<double>()), g.P);
                Assert.Equal(a["fill"]!.GetValue<bool>(), g.Fill);
                Assert.Equal(a["fc"]!.GetValue<string>(), g.Fc);
                Assert.Equal(a["close"]!.GetValue<bool>(), g.Close);
                Assert.Equal(a["align"]!.GetValue<string>(), g.Align);
                Assert.Equal(a["aufrecht"]!.GetValue<bool>(), g.Aufrecht);
                Assert.Equal(a["w"]!.GetValue<double>(), g.W);
                Assert.Equal(a["dash"]!.GetValue<bool>(), g.Dash);
                if (g.T == "text") { Assert.Equal(a["s"]!.GetValue<string>(), g.S); Assert.Equal(a["size"]!.GetValue<double>(), g.Size); Assert.Equal(a["bold"]!.GetValue<bool>(), g.Bold); }
            }
            var aq = qs["anschluesse"]!.AsArray();
            Assert.Equal(aq.Count, s.Anschluesse.Count);
            for (int i = 0; i < aq.Count; i++)
            {
                Assert.Equal(aq[i]!["id"]!.GetValue<string>(), s.Anschluesse[i].Id);
                Assert.Equal(aq[i]!["x"]!.GetValue<double>(), s.Anschluesse[i].X);
                Assert.Equal(aq[i]!["y"]!.GetValue<double>(), s.Anschluesse[i].Y);
                Assert.Equal(aq[i]!["dir"]!.GetValue<string>(), s.Anschluesse[i].Dir);
            }
        }
    }

    [Fact]
    public void Katalogkopf_Nennt_Quelle_Pruefsumme_Und_Datum()
    {
        var k = Sk.Kopf;
        // Prüfsumme der Quelldatei: die Kopie in tests/daten ist byte-gleich zur Testkopie, aus der der Katalog erzeugt wurde
        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(IfcHilfe.Testdaten(Quelle)))).ToLowerInvariant();
        Assert.Equal(sha, k.QuelleDaten!["sha256"]!.GetValue<string>());
        Assert.Equal("2026-10-14", k.QuelleDaten["geaendert"]!.GetValue<string>());
        Assert.Equal(155, k.QuelleDaten["eintraege"]!.GetValue<int>());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", k.Erzeugt);
        Assert.Equal(64, k.Inhaltspruefsumme.Length);
    }

    [Fact]
    public void Katalog_Wird_Nur_Mit_Stimmender_Pruefsumme_Geladen()
    {
        var datei = Path.Combine(Katalog.StandardOrdner(), SchemaSymbolKatalog.Datei);
        var text = File.ReadAllText(datei);
        var geladen = SchemaSymbolKatalog.Laden(datei);
        Assert.Equal(155, geladen.Symbole.Count);

        var tmp = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "sk_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            // Zeilenenden (Git) ändern die Prüfsumme nicht
            var crlf = Path.Combine(tmp, "crlf.json");
            File.WriteAllText(crlf, text.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            Assert.Equal(155, SchemaSymbolKatalog.Laden(crlf).Symbole.Count);

            // Handänderung an einer Geometriezahl, an der Zuordnung, am Massstab: abgelehnt
            foreach (var (alt, neu) in new[] { ("\"massstab\": 1,", "\"massstab\": 2,"), ("\"symbol\": \"kugelhahn\"", "\"symbol\": \"klappe\""), ("\"label\": \"Kugelhahn\"", "\"label\": \"Kugelhahn!\"") })
            {
                Assert.Contains(alt, text);
                var f = Path.Combine(tmp, "geaendert.json");
                File.WriteAllText(f, text.Replace(alt, neu));
                var e = Assert.Throws<HlksFehler>(() => SchemaSymbolKatalog.Laden(f));
                Assert.Contains("Prüfsumme", e.Message);
                Assert.Contains("hkap_symbole_uebernehmen", e.Vorschlag);
            }
        }
        finally { Directory.Delete(tmp, true); }
    }

    [Fact]
    public void Pruefsumme_Im_Kopf_Ist_Die_Der_Drei_Abschnitte()
    {
        var w = JsonNode.Parse(File.ReadAllText(Path.Combine(Katalog.StandardOrdner(), SchemaSymbolKatalog.Datei)))!;
        Assert.Equal(w["kopf"]!["inhaltspruefsumme"]!.GetValue<string>(), SchemaSymbolKatalog.Pruefsumme(w["darstellung"]!, w["zuordnung"]!, w["symbole"]!));
        // ein Zeichen im Abschnitt symbole ändert sie
        w["symbole"]![0]!["label"] = w["symbole"]![0]!["label"]!.GetValue<string>() + "x";
        Assert.NotEqual(w["kopf"]!["inhaltspruefsumme"]!.GetValue<string>(), SchemaSymbolKatalog.Pruefsumme(w["darstellung"]!, w["zuordnung"]!, w["symbole"]!));
    }

    [Fact]
    public void Zuordnung_Verweist_Nur_Auf_Sichtbare_Symbole_Und_Alle_Sparten_Sind_Im_Katalog()
    {
        foreach (var z in Sk.Zuordnung.Where(z => z.Symbol is not null))
        {
            var s = Sk.Symbol(z.Symbol!);
            Assert.NotNull(s);
            Assert.False(s!.Versteckt, $"{z.Klasse}/{z.Art} verweist auf das ausgeblendete Symbol {z.Symbol}");
        }
        // jede offene Zeile (kein Symbol) erklärt sich in der Annahme
        foreach (var z in Sk.Zuordnung.Where(z => z.Symbol is null)) Assert.Contains("Offen", z.Annahme);
        // Entscheide Christians vom 21.10.2026
        Assert.Equal("wt_platte", Zeile("Waermeerzeuger", "fernwaerme").Symbol);
        Assert.Equal("kugelhahn", Zeile("Ventil", "absperrventil").Symbol);
        Assert.Equal("wt_platte", Zeile("Waermetauscher", null).Symbol);
        var kaelte = Sk.Zuordnung.Single(z => z.Klasse == "Waermetauscher" && z.Kaelte == true);
        Assert.Equal("wt_platte", kaelte.Symbol);
        Assert.StartsWith("Kälte: Raute wie Heizung, bestätigen", kaelte.Annahme);
        // Sichtbarkeit: Kugelhahn und Wärmetauscher sind angepasste Symbole Christians
        Assert.EndsWith("(angepasst)", Sk.Symbol("kugelhahn")!.Herkunft);
        Assert.EndsWith("(angepasst)", Sk.Symbol("wt_platte")!.Herkunft);
    }

    static SymbolZuordnung Zeile(string klasse, string? art) => Sk.Zuordnung.First(z => z.Klasse == klasse && z.Art == art && z.Kaelte != true);

    // ------------------------------------------------------------------ Zuordnung am Projekt

    static Projekt Leer() => Projekt.Anlegen(Kat, null, "Test Schemasymbole (fiktiv)", "T", "pc");

    static Element Anlegen(Projekt p, string klasse, string kz, string? art = null, string? systemtyp = null)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (systemtyp is not null)
        {
            var sys = "S-" + kz;
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = sys, ["eigenschaften"] = new JsonObject { ["systemtyp"] = systemtyp } }));
            o["system"] = sys;
        }
        if (art is not null) o["eigenschaften"] = new JsonObject { ["art"] = art };
        Ok(p.Ausfuehren("element_anlegen", o));
        return Hole(p, kz);
    }

    [Theory]
    [InlineData("Waermeerzeuger", "oel", "kessel")]
    [InlineData("Waermeerzeuger", "gas", "eig-gas_heizkessel")]
    [InlineData("Waermeerzeuger", "holz", "eig-holz_heizkessel__kopie")]
    [InlineData("Waermeerzeuger", "waermepumpe", "eig-oel_heizkessel__kopie")]
    [InlineData("Waermeerzeuger", "fernwaerme", "wt_platte")]
    public void Waermeerzeuger_Je_Art(string klasse, string art, string erwartet)
    {
        var p = Leer();
        var e = Anlegen(p, klasse, "WEZ-1", art);
        Assert.Equal(erwartet, SchemaSymbole.KatalogSymbolVon(p, e)?.Key);
        Assert.Equal("katalogsymbol", SchemaSymbole.Schluessel(p, e));
    }

    [Fact]
    public void Speicher_Heizung_Und_Kaelte()
    {
        var p = Leer();
        Assert.Equal("speicher", SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Speicher", "SP-1", null, "Heizung Vorlauf"))?.Key);
        Assert.Equal("eig-energiespeicher_kaelte", SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Speicher", "SP-2", null, "Kälte Vorlauf"))?.Key);
        Assert.Equal("speicher", SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Speicher", "SP-3"))?.Key);   // ohne System: Heizung
    }

    [Theory]
    [InlineData("absperrventil", "Heizung Vorlauf", "kugelhahn")]
    [InlineData("absperrventil", "Kälte Rücklauf", "kugelhahn")]
    [InlineData("regulierventil", "Heizung Rücklauf", "eig-absperrventil___kopie-2")]
    [InlineData("rueckflussverhinderer", "Heizung Vorlauf", "eig-rueckschlagklappe")]
    [InlineData("magnetventil", "Heizung Vorlauf", "eig-absperrventil")]
    [InlineData("mischventil", "Heizung Vorlauf", "eig-3_wegventil")]
    public void Ventil_Je_Art_Nur_In_Heizung_Und_Kaelte(string art, string systemtyp, string erwartet)
    {
        var p = Leer();
        Assert.Equal(erwartet, SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Ventil", "V-1", art, systemtyp))?.Key);
    }

    [Theory]
    [InlineData("Trinkwasser kalt")]
    [InlineData("Trinkwasser warm")]
    [InlineData("Schmutzwasser")]
    [InlineData("Zuluft")]
    public void Ventil_Und_Pumpe_Im_Sanitaer_Oder_Ohne_System_Behalten_Die_Bisherige_Darstellung(string systemtyp)
    {
        var p = Leer();
        var v = Anlegen(p, "Ventil", "V-1", "absperrventil", systemtyp);
        Assert.Null(SchemaSymbole.KatalogSymbolVon(p, v));
        Assert.Equal("ventil", SchemaSymbole.Schluessel(p, v));
        var pu = Anlegen(p, "Pumpe", "P-1", null, systemtyp);
        Assert.Null(SchemaSymbole.KatalogSymbolVon(p, pu));
        Assert.Equal("pumpe", SchemaSymbole.Schluessel(p, pu));
    }

    [Fact]
    public void Pumpe_In_Heizung_Und_Kaelte()
    {
        var p = Leer();
        Assert.Equal("pumpe", SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Pumpe", "P-2", null, "Heizung Vorlauf"))?.Key);
        Assert.Equal("pumpe", SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Pumpe", "P-3", null, "Kälte Vorlauf"))?.Key);
    }

    [Fact]
    public void Offene_Zeilen_Und_Art_Ohne_Zeile_Bleiben_Bei_Der_Bisherigen_Darstellung()
    {
        var p = Leer();
        // druckreduzierventil: Zeile ausdrücklich offen (Grundsymbol ausgeblendet)
        var e = Anlegen(p, "Ventil", "V-2", "druckreduzierventil", "Heizung Vorlauf");
        Assert.Null(SchemaSymbole.KatalogSymbolVon(p, e));
        Assert.Equal("ventil", SchemaSymbole.Schluessel(p, e));
        // Klasse ohne Zuordnung (Volumenstromregler)
        Assert.Null(SchemaSymbole.KatalogSymbolVon(p, Anlegen(p, "Volumenstromregler", "VR-1", null, "Heizung Vorlauf")));
    }

    [Fact]
    public void Fehlende_Klassen_Im_Klassenkatalog_Lassen_Die_Zeilen_Ins_Leere_Laufen()
    {
        // Mischventil, Ausdehnungsgefaess, Waermetauscher, Fuehler, Kaelteerzeuger gehören zu einem parallelen Auftrag: fehlen sie, greift die Zeile nicht und nichts stürzt ab
        var p = Leer();
        foreach (var klasse in new[] { "Mischventil", "Ausdehnungsgefaess", "Waermetauscher", "Fuehler", "Kaelteerzeuger" })
            if (!Kat.Klassen.ContainsKey(klasse)) Assert.Contains(Sk.Zuordnung, z => z.Klasse == klasse);   // die Zeilen sind vorbereitet
        var e = Anlegen(p, "Speicher", "SP-9");
        Assert.NotNull(Sk.Waehle(p, e));
    }

    [Fact]
    public void Neue_Klassen_Werden_Zugeordnet_Wenn_Der_Katalog_Sie_Kennt()
    {
        // Klassen des parallelen Auftrags (Modul 5 A-D) in einem eigenen Katalog nachbilden; Elemente: Kopien eines Speichers mit anderer Klasse
        var kat = Katalog.Laden(Katalog.StandardOrdner());
        foreach (var n in new[] { "Waermetauscher", "Kaelteerzeuger", "Ausdehnungsgefaess" })
            if (!kat.Klassen.ContainsKey(n)) kat.Klassen[n] = new KlassenDef { Name = n, Kuerzel = n[..2].ToUpperInvariant() };
        var p = Projekt.Anlegen(kat, null, "Test neue Klassen (fiktiv)", "T", "pc");
        void Pruefe(string klasse, string systemtyp, string erwartet)
        {
            var basis = Anlegen(p, "Speicher", "SP-" + klasse + systemtyp.Length, null, systemtyp).Kopie();
            basis.Klasse = klasse;
            Assert.Equal(erwartet, SchemaSymbole.KatalogSymbolVon(p, basis)?.Key);
        }
        Pruefe("Waermetauscher", "Heizung Vorlauf", "wt_platte");
        Pruefe("Waermetauscher", "Kälte Vorlauf", "wt_platte");
        Pruefe("Kaelteerzeuger", "Heizung Vorlauf", "eig-kaeltemaschine");
        Pruefe("Ausdehnungsgefaess", "Heizung Vorlauf", "eig-expansionsgefaess_sd");
        Pruefe("Ausdehnungsgefaess", "Kälte Vorlauf", "eig-expansionsgefaess_sd_kaelte");
    }
}
