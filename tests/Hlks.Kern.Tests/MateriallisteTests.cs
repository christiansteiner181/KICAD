using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, B4: Leitungslängen je Serie und Grösse (Ableitungen.Leitungslaengen), Mengen und Strangliste mit «laengen» (bestehende
/// Felder bleiben), Materialliste (Leitungen je Serie und Grösse in m, Formteile je Familie und Typ, Bögen je Winkel; Filter system, strang,
/// geschoss). Werte FIKTIV (Testrohrserie da = DN + 5; Testbogen, Test-T-Stück).</summary>
public class MateriallisteTests
{
    static double Zahl(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException($"Keine Zahl: {n?.ToJsonString() ?? "null"}");
    static string Text(JsonNode? n) => n?.GetValue<string>() ?? "";
    static JsonArray Liste(JsonNode? n) => n as JsonArray ?? throw new Xunit.Sdk.XunitException("Keine Liste");

    static BefehlsErgebnis RohrIn(Projekt p, string kz, string typ, string? strang, string geschoss, string system, params double[][] punkte)
    {
        var o = new JsonObject
        {
            ["klasse"] = "Rohr", ["kennzeichen"] = kz, ["system"] = system, ["geschoss"] = geschoss,
            ["eigenschaften"] = new JsonObject { ["familientyp"] = typ }, ["bezuege"] = new JsonObject { ["familie"] = "FAM-1" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(punkte) },
        };
        if (strang is not null) o["teilVon"] = strang;
        return p.Ausfuehren("element_anlegen", o);
    }

    /// <summary>Projekt mit Serie und Formteilen, Systemen KW und WW, Strang ST-1 (KW), Geschossen EG und 1.OG.</summary>
    static Projekt Projekt3()
    {
        var p = MitSerie();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "WW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Trinkwasser warm" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "ST-1", ["teilVon"] = "KW", ["eigenschaften"] = new JsonObject { ["art"] = "steigstrang" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "1.OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 } }));
        return p;
    }

    // ------------------------------------------------------------------ Leitungslängen

    [Fact]
    public void Leitungslaengen_ZweiGeraden3000Und1450_Ergeben4Komma45m()
    {
        var p = Projekt3();
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [3000, 0, 500], [4450, 0, 500]));
        var l = Ableitungen.Leitungslaengen(p);
        var e = Assert.Single(l);
        Assert.Equal("Testrohrserie (fiktiv)", e.Serie);
        Assert.Equal("NW 20", e.Groesse);   // Entscheid Christian 19.10.2026 (B9b): Rohre ausser Abwasser mit NW (Vorgabe) statt Typname
        Assert.Equal(4.45, e.LaengeM, 9);
    }

    [Fact]
    public void Leitungslaengen_PolylinieIstSummeDerTeilstuecke_Achslaenge3D()
    {
        var p = Projekt3();
        Ok(Rohr(p, "RO-1", "DN 25", [0, 0, 0], [3000, 0, 0], [3000, 4000, 0]));
        Ok(Rohr(p, "RO-2", "DN 25", [0, 100, 0], [0, 100, 1500]));     // senkrecht: 1.5 m
        Assert.Equal(8.5, Assert.Single(Ableitungen.Leitungslaengen(p)).LaengeM, 9);
    }

    [Fact]
    public void Leitungslaengen_JeSerieUndGroesse_OhneSerieZuletzt_Sortiert()
    {
        var p = Projekt3();
        Ok(Rohr(p, "RO-1", "DN 32", [0, 0, 0], [1000, 0, 0]));
        Ok(Rohr(p, "RO-2", "DN 15", [0, 100, 0], [2000, 100, 0]));
        Ok(Rohr(p, "RO-3", "DN 15", [0, 200, 0], [500, 200, 0]));
        Ok(RohrOhneSerie(p, "RO-4", 100, [0, 300, 0], [4000, 300, 0]));
        Ok(RohrOhneSerie(p, "RO-5", 50, [0, 400, 0], [1000, 400, 0]));
        var l = Ableitungen.Leitungslaengen(p);
        Assert.Equal(
            [("Testrohrserie (fiktiv)", "NW 15", 2.5), ("Testrohrserie (fiktiv)", "NW 32", 1.0), ("ohne Serie", "NW 50", 1.0), ("ohne Serie", "NW 100", 4.0)],
            l.Select(x => (x.Serie, x.Groesse, x.LaengeM)).ToArray());
    }

    [Fact]
    public void Leitungslaengen_Filter_StrangSystemGeschoss()
    {
        var p = Projekt3();
        Ok(RohrIn(p, "RO-1", "DN 20", "ST-1", "EG", "KW", [0, 0, 500], [2000, 0, 500]));
        Ok(RohrIn(p, "RO-2", "DN 20", null, "1.OG", "KW", [0, 100, 3500], [1000, 100, 3500]));
        Ok(RohrIn(p, "RO-3", "DN 25", null, "EG", "WW", [0, 200, 500], [3000, 200, 500]));
        Assert.Equal(6.0, Ableitungen.Leitungslaengen(p).Sum(x => x.LaengeM), 9);
        Assert.Equal([2.0], Ableitungen.Leitungslaengen(p, strangId: "ST-1").Select(x => x.LaengeM).ToArray());
        Assert.Equal(3.0, Ableitungen.Leitungslaengen(p, systemId: "KW").Sum(x => x.LaengeM), 9);
        Assert.Equal([("NW 25", 3.0)], Ableitungen.Leitungslaengen(p, systemId: "WW").Select(x => (x.Groesse, x.LaengeM)).ToArray());
        Assert.Equal(5.0, Ableitungen.Leitungslaengen(p, geschossId: "EG").Sum(x => x.LaengeM), 9);
        Assert.Equal([1.0], Ableitungen.Leitungslaengen(p, geschossId: "1.OG", systemId: "KW").Select(x => x.LaengeM).ToArray());
        // Id geht auch
        Assert.Equal(2.0, Ableitungen.Leitungslaengen(p, strangId: Hole(p, "ST-1").Id).Sum(x => x.LaengeM), 9);
    }

    // ------------------------------------------------------------------ Mengen und Strangliste

    [Fact]
    public void Mengen_BestehendeFelderBleiben_UndLaengenJeSerieUndGroesse()
    {
        var p = Projekt3();
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [3000, 0, 500], [4450, 0, 500]));
        Ok(RohrOhneSerie(p, "RO-3", 100, [0, 300, 0], [4000, 300, 0]));
        var o = Ableitungen.Mengen(p);
        Assert.Equal(8.45, Zahl(o["leitungslaengeTotal_m"]), 6);
        Assert.Equal(2, Liste(o["leitungen"]).Count);   // bisherige Gruppen (System, Klasse, Material, DN)
        var laengen = Liste(o["laengen"]);
        Assert.Equal(["NW 20", "NW 100"], laengen.Select(z => Text(z!["groesse"])).ToArray());
        Assert.Equal(4.45, Zahl(laengen[0]!["laenge_m"]), 6);
        Assert.Equal("Testrohrserie (fiktiv)", Text(laengen[0]!["serie"]));
        Assert.Equal("ohne Serie", Text(laengen[1]!["serie"]));
        Assert.Equal(4.0, Zahl(laengen[1]!["laenge_m"]), 6);
        Assert.NotNull(o["bauteile"]);
        Assert.Equal(8.45, Zahl(Liste(Ableitungen.Mengen(p, "KW")["laengen"]).Sum(z => Zahl(z!["laenge_m"]))), 6);
    }

    [Fact]
    public void Strangliste_LaengenJeStrang_BestehendeFelderBleiben()
    {
        var p = Projekt3();
        Ok(RohrIn(p, "RO-1", "DN 20", "ST-1", "EG", "KW", [0, 0, 500], [3000, 0, 500]));
        Ok(RohrIn(p, "RO-2", "DN 20", "ST-1", "EG", "KW", [3000, 0, 500], [4450, 0, 500]));
        Ok(RohrIn(p, "RO-3", "DN 25", "ST-1", "EG", "KW", [0, 100, 500], [1000, 100, 500]));
        var z = Liste(Ableitungen.Strangliste(p)["zeilen"]).Single()!;
        Assert.Equal("ST-1", Text(z["strang"]));
        Assert.Equal(5.45, Zahl(z["leitungslaenge_m"]), 6);
        Assert.Equal("NW 20–25", Text(z["groesse"]));   // B9b: Feld groesse statt dn
        var l = Liste(z["laengen"]);
        Assert.Equal([("NW 20", 4.45), ("NW 25", 1.0)], l.Select(x => (Text(x!["groesse"]), Zahl(x["laenge_m"]))).ToArray());
        Assert.Equal("Testrohrserie (fiktiv)", Text(l[0]!["serie"]));
    }

    [Fact]
    public void Beispielprojekt_AlleLeitungenOhneSerie_LaengenStimmenMitDenMengen()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var l = Ableitungen.Leitungslaengen(p);
        Assert.NotEmpty(l);
        Assert.All(l, x => Assert.Equal("ohne Serie", x.Serie));
        // B9b: Rohre ausser Abwasser «NW x»; die Abwasserleitungen des Beispielprojekts haben kein da → «ohne Grösse» (kein DN als Ersatz)
        Assert.All(l, x => Assert.True(x.Groesse.StartsWith("NW ") || x.Groesse == "ohne Grösse", x.Groesse));
        Assert.Contains(l, x => x.Groesse == "ohne Grösse");
        var summe = l.Sum(x => x.LaengeM);
        Assert.Equal(Zahl(Ableitungen.Mengen(p)["leitungslaengeTotal_m"]), summe, 1);
        foreach (var s in Liste(Ableitungen.Strangliste(p)["zeilen"]))
            Assert.Equal(Zahl(s!["leitungslaenge_m"]), Liste(s["laengen"]).Sum(x => Zahl(x!["laenge_m"])), 1);
    }

    // ------------------------------------------------------------------ Materialliste

    [Fact]
    public void Materialliste_Kopf_LeitungenUndLaengen()
    {
        var p = Projekt3();
        Ok(Rohr(p, "RO-1", "DN 20", [0, 0, 500], [3000, 0, 500]));
        Ok(Rohr(p, "RO-2", "DN 20", [3000, 0, 500], [4450, 0, 500]));
        Ok(Rohr(p, "RO-3", "DN 25", [0, 100, 500], [1000, 100, 500]));
        var o = Ableitungen.Materialliste(p);
        Assert.Equal("Materialliste", Text(o["ableitung"]));
        Assert.Equal(p.Kopf.Name, Text(o["projekt"]));
        Assert.Equal(p.Modell.Pruefsumme(), Text(o["modellStand"]!["pruefsumme"]));
        Assert.NotNull(o["erzeugt"]);
        var l = Liste(o["leitungen"]);
        Assert.Equal(2, l.Count);
        Assert.Equal("NW 20", Text(l[0]!["groesse"]));
        Assert.Equal(4.45, Zahl(l[0]!["laenge_m"]), 9);
        Assert.Equal(2, (int)Zahl(l[0]!["stueck"]));
        Assert.Equal("Testrohrserie (fiktiv)", Text(l[0]!["serie"]));
        Assert.Equal(1.0, Zahl(l[1]!["laenge_m"]), 9);
        Assert.Equal(5.45, Zahl(o["leitungslaengeTotal_m"]), 9);
        Assert.Empty(Liste(o["formteile"]));
        Assert.Null(o["hinweis"]);   // alle mit Serie
    }

    [Fact]
    public void Materialliste_FormteileJeFamilieTypUndWinkel()
    {
        var p = Projekt3();
        Ok(Formteil(p, "FAM-2", "DN 20", "BO-1", [0, 0, 500], 90));
        Ok(Formteil(p, "FAM-2", "DN 20", "BO-2", [1000, 0, 500], 90));
        Ok(Formteil(p, "FAM-2", "DN 20", "BO-3", [2000, 0, 500], 45));
        Ok(Formteil(p, "FAM-2", "DN 25", "BO-4", [3000, 0, 500], 90));
        Ok(Formteil(p, "FAM-3", "DN 20", "TS-1", [4000, 0, 500]));
        Ok(Formteil(p, "FAM-3", "DN 20", "TS-2", [5000, 0, 500]));
        var bogen = Hole(p, "FAM-2").Name; var ts = Hole(p, "FAM-3").Name;
        Assert.Equal(90, Hole(p, "BO-1").Zahl("winkel"));   // Exemplarwert der Familie am Element
        var o = Ableitungen.Materialliste(p);
        var z = Liste(o["formteile"]).Select(x => (Text(x!["familie"]), Text(x["typ"]), Text(x["klasse"]), Json.Zahl(x["winkel"]), (int)Zahl(x["stueck"]))).ToArray();
        Assert.Equal(
            [(ts, "DN 20", "TStueck", (double?)null, 2), (bogen, "DN 20", "Bogen", (double?)45, 1), (bogen, "DN 20", "Bogen", (double?)90, 2), (bogen, "DN 25", "Bogen", (double?)90, 1)],   // nach Familienname
            z);
        Assert.Equal(6, (int)Zahl(o["formteileTotal"]));
        Assert.Empty(Liste(o["leitungen"]));
    }

    [Fact]
    public void Materialliste_FormteileOhneFamilie_UnterOhneFamilie()
    {
        var p = Projekt3();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Bogen", ["kennzeichen"] = "BO-X", ["system"] = "KW", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["dn"] = 32, ["winkel"] = 90 },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([0, 0, 500]) },
        }));
        Ok(Formteil(p, "FAM-2", "DN 20", "BO-1", [0, 100, 500], 90));
        var f = Liste(Ableitungen.Materialliste(p)["formteile"]);
        Assert.Equal(2, f.Count);
        Assert.Equal("ohne Familie", Text(f[1]!["familie"]));    // zuletzt
        Assert.Equal("Bogen DN 32", Text(f[1]!["typ"]));
        Assert.Equal(90, Zahl(f[1]!["winkel"]));
    }

    [Fact]
    public void Materialliste_Filter_SystemStrangGeschoss()
    {
        var p = Projekt3();
        Ok(RohrIn(p, "RO-1", "DN 20", "ST-1", "EG", "KW", [0, 0, 500], [2000, 0, 500]));
        Ok(RohrIn(p, "RO-2", "DN 20", null, "1.OG", "KW", [0, 100, 3500], [1000, 100, 3500]));
        Ok(RohrIn(p, "RO-3", "DN 25", null, "EG", "WW", [0, 200, 500], [3000, 200, 500]));
        Ok(Formteil(p, "FAM-2", "DN 20", "BO-1", [2000, 0, 500], 90));                          // KW, EG
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject                                    // WW
        {
            ["familie"] = "FAM-2", ["typ"] = "DN 25", ["kennzeichen"] = "BO-2", ["system"] = "WW", ["geschoss"] = "EG",
            ["punkt"] = new JsonArray(3000, 200, 500), ["parameter"] = new JsonObject { ["winkel"] = 45 },
        }));
        Assert.Equal(6.0, Zahl(Ableitungen.Materialliste(p)["leitungslaengeTotal_m"]), 9);
        Assert.Equal(2, (int)Zahl(Ableitungen.Materialliste(p)["formteileTotal"]));

        var kw = Ableitungen.Materialliste(p, system: "KW");
        Assert.Equal(3.0, Zahl(kw["leitungslaengeTotal_m"]), 9);
        Assert.Equal(1, (int)Zahl(kw["formteileTotal"]));
        Assert.Equal("KW", Text(kw["filter"]!["system"]));

        var strang = Ableitungen.Materialliste(p, strang: "ST-1");
        Assert.Equal(2.0, Zahl(strang["leitungslaengeTotal_m"]), 9);
        Assert.Equal(0, (int)Zahl(strang["formteileTotal"]));

        var og = Ableitungen.Materialliste(p, geschoss: "1.OG");
        Assert.Equal(1.0, Zahl(og["leitungslaengeTotal_m"]), 9);

        var ww = Ableitungen.Materialliste(p, "WW", null, "EG");
        Assert.Equal(3.0, Zahl(ww["leitungslaengeTotal_m"]), 9);
        Assert.Equal(45, Zahl(Liste(ww["formteile"])[0]!["winkel"]));
    }

    [Fact]
    public void Materialliste_UnbekannterFilter_FehlerMitVorschlag()
    {
        var p = Projekt3();
        foreach (var (s, st, g) in new (string?, string?, string?)[] { ("XYZ", null, null), (null, "ST-9", null), (null, null, "9.OG"), ("EG", null, null), (null, "KW", null) })
        {
            var f = Assert.Throws<HlksFehler>(() => { Ableitungen.Materialliste(p, s, st, g); });
            Assert.False(string.IsNullOrWhiteSpace(f.Message));
            Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag), f.Message);
        }
        // über die Abfrage-Tür (Oberfläche, MCP)
        var par = new JsonObject { ["system"] = "XYZ" };
        Assert.Throws<HlksFehler>(() => { LeitungenAbfragen.Tuer(p, "materialliste", par); });
        var ok = (JsonObject)LeitungenAbfragen.Tuer(p, "materialliste", new JsonObject { ["system"] = "KW" })!;
        Assert.Equal("Materialliste", Text(ok["ableitung"]));
    }

    [Fact]
    public void Materialliste_Deterministisch_UndOhneSerieMitHinweis()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        string Ohne(JsonObject o) { o.Remove("erzeugt"); return o.ToJsonString(); }
        var a = Ableitungen.Materialliste(p);
        Assert.Equal(Ohne(a), Ohne(Ableitungen.Materialliste(p)));
        Assert.Contains("keine Serie", Text(a["hinweis"]));
        Assert.All(Liste(a["leitungen"]), z => Assert.Equal("ohne Serie", Text(z!["serie"])));
        Assert.Equal(Zahl(Ableitungen.Mengen(p)["leitungslaengeTotal_m"]), Zahl(a["leitungslaengeTotal_m"]), 6);
        // Formteile des Beispielprojekts haben keine Familie
        Assert.NotEmpty(Liste(a["formteile"]));
        Assert.All(Liste(a["formteile"]), z => Assert.Equal("ohne Familie", Text(z!["familie"])));
        // Reihenfolge der Gruppen unabhängig von der Anlegereihenfolge
        var p2 = Projekt3();
        Ok(Rohr(p2, "RO-1", "DN 50", [0, 0, 0], [1000, 0, 0]));
        Ok(Rohr(p2, "RO-2", "DN 15", [0, 100, 0], [1000, 100, 0]));
        Assert.Equal(["NW 15", "NW 50"], Liste(Ableitungen.Materialliste(p2)["leitungen"]).Select(z => Text(z!["groesse"])).ToArray());
    }

    [Fact]
    public void Materialliste_Abfrage_IstEingehaengt()
    {
        var def = Abfragen.Liste.Single(a => a.Name == "materialliste");
        Assert.Equal(["system", "strang", "geschoss"], def.Parameter.Select(x => x.Name).ToArray());
        Assert.All(def.Parameter, x => Assert.False(x.Pflicht));
    }

    // ------------------------------------------------------------------ Korrektur 30a: Abzweigwinkel und Bogenwinkel je Zeile

    /// <summary>Projekt mit System SW und der Testabwasserserie samt Bogen (FAM-2) und Abwasser-T (FAM-3, Abzweigwinkel als Exemplarparameter
    /// winkel, Vorgabe 88.5). FIKTIV.</summary>
    static Projekt MitAbwasser()
    {
        var p = Neu();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "SW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Schmutzwasser" } }));
        foreach (var f in new[] { "test_abwasserserie", "test_abwasserbogen", "test_abwasser_t" })
            Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = Datei(f) }));
        return p;
    }

    static BefehlsErgebnis AwFormteil(Projekt p, string familie, string typ, string kz, double x, double? winkel)
    {
        var par = new JsonObject
        {
            ["familie"] = familie, ["typ"] = typ, ["kennzeichen"] = kz, ["system"] = "SW", ["geschoss"] = "EG",
            ["punkt"] = new JsonArray(x, 0, 500),
        };
        if (winkel is double w) par["parameter"] = new JsonObject { ["winkel"] = w };
        return p.Ausfuehren("familie_platzieren", par);
    }

    static (string Typ, string Klasse, double? Winkel, int Stueck)[] FormteilZeilen(JsonObject ml) =>
        Liste(ml["formteile"]).Select(z => (Text(z!["typ"]), Text(z["klasse"]), Json.Zahl(z["winkel"]), (int)Zahl(z["stueck"]))).ToArray();

    [Fact]
    public void Materialliste_TStueckeJeAbzweigwinkel_45Und88Komma5SindZweiZeilen()
    {
        var p = MitAbwasser();
        Ok(AwFormteil(p, "FAM-3", "DN 100", "TS-1", 0, 45));
        Ok(AwFormteil(p, "FAM-3", "DN 100", "TS-2", 1000, 88.5));
        Ok(AwFormteil(p, "FAM-3", "DN 100", "TS-3", 2000, 45));
        Ok(AwFormteil(p, "FAM-3", "DN 100", "TS-4", 3000, null));       // Vorgabe der Familie: 88.5
        Ok(AwFormteil(p, "FAM-3", "DN 100/50", "TS-5", 4000, 45));      // andere Grösse, 45°
        var ml = Ableitungen.Materialliste(p);
        Assert.Equal(
            [("DN 100", "TStueck", (double?)45, 2), ("DN 100", "TStueck", (double?)88.5, 2), ("DN 100/50", "TStueck", (double?)45, 1)],
            FormteilZeilen(ml));
        Assert.Equal(5, (int)Zahl(ml["formteileTotal"]));
    }

    [Fact]
    public void Materialliste_BoegenJeWinkel_UndTStueckeOhneWinkelparameterBleibenOhneWinkel()
    {
        var p = MitAbwasser();
        Ok(AwFormteil(p, "FAM-2", "DN 100", "BO-1", 0, 45));
        Ok(AwFormteil(p, "FAM-2", "DN 100", "BO-2", 1000, 88.5));
        Ok(AwFormteil(p, "FAM-2", "DN 100", "BO-3", 2000, 45));
        Ok(AwFormteil(p, "FAM-3", "DN 100", "TS-1", 3000, 88.5));
        var z = FormteilZeilen(Ableitungen.Materialliste(p));
        Assert.Equal(
            [("DN 100", "Bogen", (double?)45, 2), ("DN 100", "Bogen", (double?)88.5, 1), ("DN 100", "TStueck", (double?)88.5, 1)],
            z.Select(x => (x.Typ, x.Klasse, x.Winkel, x.Stueck)).OrderBy(x => x.Klasse, StringComparer.Ordinal).ThenBy(x => x.Winkel).ToArray());
        // Test-T-Stück ohne Winkelparameter (FAM-3 der Rohrserie): ein Abzweig, kein Winkel – gleiche Zeile für alle
        var q = Projekt3();
        Ok(Formteil(q, "FAM-3", "DN 20", "TS-1", [0, 0, 500]));
        Ok(Formteil(q, "FAM-3", "DN 20", "TS-2", [1000, 0, 500]));
        var zeile = Assert.Single(FormteilZeilen(Ableitungen.Materialliste(q)));
        Assert.Equal(("DN 20", "TStueck", (double?)null, 2), zeile);
    }

    [Fact]
    public void Materialliste_JsonEnthaeltDenWinkelAnJederFormteilzeile_FuerDieAusgabe()
    {
        var p = MitAbwasser();
        Ok(AwFormteil(p, "FAM-3", "DN 100", "TS-1", 0, 45));
        Ok(AwFormteil(p, "FAM-2", "DN 100", "BO-1", 1000, 45));
        var o = Ableitungen.Materialliste(p);
        var json = o.ToJsonString();
        Assert.Contains("\"winkel\":45", json);
        Assert.All(Liste(o["formteile"]), z => Assert.Equal(45.0, Zahl(z!["winkel"])));
        Assert.Contains("Abzweigwinkel", Ableitungen.MateriallisteText);
    }
}
