using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaansichtHilfe;
using static Hlks.Kern.Tests.SchemaStrangschemaHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B8 «Strangschema nach Beispiel» (entwurf/modul-3.md, Abnahme Christian 19.10.2026): Vorlagen,
/// Bündel je Steigzone nach realer Lage und Blickrichtung, Gruppen nach Lage im Gebäude, Endgeräte nach Netzreihenfolge,
/// Wohnungsblöcke mit Tabelle und Zählern, Deckenbänder, Dämmung, Kreise. Alle Werte FIKTIV; Lagen von Hand nach SchemaRaster
/// (Papier-mm, y nach oben) gerechnet.</summary>
public class SchemaStrangschemaTests(ITestOutputHelper aus)
{
    const string An = "AN-B8";

    // ------------------------------------------------------------------ kleine Netze

    /// <summary>Geschosse UG (−2700) und EG (0), Systeme KW, WW, ZIR; je Strang ein T im UG und im EG, verbunden durch eine senkrechte
    /// Leitung bei (x, y) – die Steigachse des Strangs. Strang ohne x: ohne Bauteile (nicht platziert). Ansicht AN-B8 (Schema).</summary>
    static Projekt Steigzonen(params (string Kz, string System, string? Zone, double? X, double Y)[] straenge)
    {
        var p = Projekt.Anlegen(Kat, null, "Steigzonen (fiktiv)", "T", "pc");
        void A(string b, JsonObject o) => Ok(p.Ausfuehren(b, o));
        A("element_anlegen", Par(("klasse", "Gebaeude"), ("kennzeichen", "GEB")));
        A("element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", "UG"), ("teilVon", "GEB"), ("eigenschaften", new JsonObject { ["kote"] = -2700, ["hoehe"] = 2700 })));
        A("element_anlegen", Par(("klasse", "Geschoss"), ("kennzeichen", "EG"), ("teilVon", "GEB"), ("eigenschaften", new JsonObject { ["kote"] = 0, ["hoehe"] = 2900 })));
        foreach (var (kz, typ) in new[] { ("KW", "Trinkwasser kalt"), ("WW", "Trinkwasser warm"), ("ZIR", "Zirkulation") })
            A("element_anlegen", Par(("klasse", "System"), ("kennzeichen", kz), ("eigenschaften", new JsonObject { ["systemtyp"] = typ })));
        foreach (var (kz, sys, zone, x, y) in straenge)
        {
            var eig = new JsonObject { ["art"] = "steigstrang" };
            if (zone is not null) eig["steigzone"] = zone;
            A("element_anlegen", Par(("klasse", "Strang"), ("kennzeichen", kz), ("teilVon", sys), ("eigenschaften", eig)));
            if (x is not double xs) continue;
            A("element_anlegen", Par(("klasse", "TStueck"), ("kennzeichen", $"T-{kz}-U"), ("teilVon", kz), ("geometrie", new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([xs, y, -2400]) })));
            A("element_anlegen", Par(("klasse", "TStueck"), ("kennzeichen", $"T-{kz}-E"), ("teilVon", kz), ("geometrie", new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([xs, y, 300]) })));
            A("leitung_ziehen", Par(("kennzeichen", $"R-{kz}"), ("strang", kz), ("dn", 20), ("von", $"T-{kz}-U.b"), ("nach", $"T-{kz}-E.a"), ("punkte", Pts([xs, y, -2400], [xs, y, 300]))));
        }
        A("element_anlegen", Par(("klasse", "Ansicht"), ("kennzeichen", An), ("eigenschaften", new JsonObject { ["art"] = "schema" })));
        return p;
    }

    static void Blick(Projekt p, string blick) =>
        Ok(p.Ausfuehren("element_aendern", Par(("element", An), ("eigenschaften", new JsonObject { ["blickrichtung"] = blick }))));

    static SchemaGruppe Gruppe(Schemaplan s, string bezeichnung) => s.Gruppen.Single(g => g.Bezeichnung == bezeichnung);
    static string[] Buendel(Schemaplan s, string bezeichnung) => Gruppe(s, bezeichnung).Spalten.Select(sp => sp.Strang!.Kennzeichen).ToArray();

    // ------------------------------------------------------------------ Vorlagen

    [Fact]
    public void Vorlagen_WasserUndAbwasser_SystemeNachMedium()
    {
        var p = Mfh();
        var w = Hole(p, Wasser);
        Assert.Equal("Schema Wasser", w.Name);
        Assert.Equal("schema", w.Text("art"));
        Assert.Equal("strangschema", w.Text("schemaart"));
        Assert.Equal(["KW", "WW", "ZIR"], Grafik.Liste(w, "systeme"));
        Assert.True(Grafik.Bool(w, "wohnungsbloecke"));
        var a = Hole(p, Abwasser);
        Assert.Equal("Schema Abwasser", a.Name);
        Assert.Equal(["RW", "SW"], Grafik.Liste(a, "systeme"));
        Assert.False(Grafik.Bool(a, "wohnungsbloecke"));
        // das Wasserschema zeigt kein Abwasser, das Abwasserschema kein Trinkwasser
        Assert.DoesNotContain(Plan(p, Wasser).Graph.Linien, l => l.Medium is "SW" or "RW");
        Assert.DoesNotContain(Plan(p, Abwasser).Graph.Linien, l => l.Medium is "KW" or "WW" or "ZIR");
        Assert.Contains(Plan(p, Abwasser).Graph.Knoten, k => k.Kennzeichen == "DAB-1");
        Assert.DoesNotContain(Plan(p, Wasser).Graph.Knoten, k => k.Kennzeichen == "DAB-1");
    }

    [Fact]
    public void Vorlagen_TrockenlaufRueckgaengigKennzeichenUndFehler()
    {
        var p = Mfh(schemas: false);
        var stand = p.Modell.Pruefsumme();
        var t = Ok(p.Ausfuehren("schema_anlegen", Par(("vorlage", "wasser")), trockenlauf: true));
        Assert.True(t.Trockenlauf);
        Assert.Equal(stand, p.Modell.Pruefsumme());
        var e = Ok(p.Ausfuehren("schema_anlegen", Par(("vorlage", "wasser"), ("blickrichtung", "nord"))));
        Assert.Contains(e.Hinweise, h => h.Contains(Wasser) && h.Contains("KW, WW, ZIR"));
        Assert.Equal("nord", Hole(p, Wasser).Text("blickrichtung"));
        Assert.Equal("nord", Plan(p, Wasser).Blickrichtung);
        // zweites Wasserschema: das Vorgabe-Kennzeichen ist vergeben, der Kern bildet eines
        var zweites = Ok(p.Ausfuehren("schema_anlegen", Par(("vorlage", "Wasser"), ("name", "Schema Wasser Variante"))));
        Assert.Contains(p.Modell.DerKlasse("Ansicht"), x => x.Name == "Schema Wasser Variante" && x.Kennzeichen != Wasser);
        Assert.NotNull(zweites.PaketId);
        Ok(p.Rueckgaengig());
        Assert.DoesNotContain(p.Modell.DerKlasse("Ansicht"), x => x.Name == "Schema Wasser Variante");
        // Fehler mit Vorschlag
        var unbekannt = Nein(p.Ausfuehren("schema_anlegen", Par(("vorlage", "heizung"))));
        Assert.Contains("wasser", unbekannt.Vorschlag);
        Assert.Contains("abwasser", unbekannt.Vorschlag);
        Assert.Contains("sued", Nein(p.Ausfuehren("schema_anlegen", Par(("vorlage", "abwasser"), ("blickrichtung", "oben")))).Vorschlag);
        var leer = Projekt.Anlegen(Kat, null, "leer", "T", "pc");
        var ohne = Nein(leer.Ausfuehren("schema_anlegen", Par(("vorlage", "abwasser"))));
        Assert.Contains("kein System", ohne.Fehler);
        Assert.Contains("Schmutzwasser", ohne.Vorschlag);
    }

    [Fact]
    public void Vorlagen_MediumAusDemModellSonstAusDemKatalog()
    {
        // Medium am System (vom Systemtyp gesetzt) bzw. ohne Eintrag aus dem Katalog: Systemtyp Zirkulation → ZIR
        var ohneMedium = new Element { Klasse = "System", Kennzeichen = "X", Eigenschaften = { ["systemtyp"] = new Wert { Inhalt = JsonValue.Create("Zirkulation") } } };
        Assert.Equal("ZIR", SchemaBefehle.MediumVon(Kat, ohneMedium));
        var p = Mfh(schemas: false);
        Assert.Equal("RW", SchemaBefehle.MediumVon(Kat, Hole(p, "RW")));
        Assert.Equal(["RW", "SW"], SchemaBefehle.SystemeDerVorlage(p.Modell, Kat, "abwasser").Select(s => s.Kennzeichen));
        Assert.Contains(p.Befehle.Alle, b => b.Name == "schema_anlegen");
    }

    // ------------------------------------------------------------------ Bündel, Gruppen, Blickrichtung

    /// <summary>PW 1: KW-1 (1000, 500), WW-1 (1050, 450), ZIR-1 (1100, 520) → Mitte (1050, 490); PW 2: KW-2 (5000, 3000), WW-2 (5060, 3000)
    /// → Mitte (5030, 3000). Quer zur Blickrichtung: von Süden x, von Norden −x, von Osten y, von Westen −y; Gleichstand (PW 2 von Osten
    /// bzw. Westen: beide y 3000) nach der Tiefe, nahe zuerst (von Osten −x: WW-2 vor KW-2; von Westen x: KW-2 vor WW-2).</summary>
    [Theory]
    [InlineData("sued", "PW 1|PW 2", "KW-1,WW-1,ZIR-1", "KW-2,WW-2")]
    [InlineData("nord", "PW 2|PW 1", "ZIR-1,WW-1,KW-1", "WW-2,KW-2")]
    [InlineData("ost", "PW 1|PW 2", "WW-1,KW-1,ZIR-1", "WW-2,KW-2")]
    [InlineData("west", "PW 2|PW 1", "ZIR-1,KW-1,WW-1", "KW-2,WW-2")]
    public void Buendel_GleicheSteigzone_NachRealerLageUndBlickrichtung(string blick, string gruppen, string pw1, string pw2)
    {
        var p = Steigzonen(("KW-1", "KW", "PW 1", 1000, 500), ("WW-1", "WW", "PW 1", 1050, 450), ("ZIR-1", "ZIR", "PW 1", 1100, 520),
            ("KW-2", "KW", "PW 2", 5000, 3000), ("WW-2", "WW", "PW 2", 5060, 3000));
        Blick(p, blick);
        var s = Plan(p, An);
        Assert.Equal(blick, s.Blickrichtung);
        Assert.Equal(gruppen.Split('|'), s.Gruppen.Select(g => g.Bezeichnung));
        Assert.Equal(pw1.Split(','), Buendel(s, "PW 1"));
        Assert.Equal(pw2.Split(','), Buendel(s, "PW 2"));
        Assert.Equal([1050.0, 490.0], Gruppe(s, "PW 1").Lage!.Select(v => Math.Round(v, 6)));
        // Stranglinien des Bündels im Bündelabstand 3 ab x0 + 10; Gruppe 35 breit (Kreis «PW 1»: 13 + 4.5 + 1 ≤ 35)
        var erste = s.Gruppen[0];
        Assert.Equal(30, erste.X0);
        Assert.Equal(35, erste.Breite);
        Assert.Equal(erste.Spalten.Count == 3 ? [40.0, 43.0, 46.0] : [40.0, 43.0], erste.Spalten.Select(sp => sp.LinieX));
        Assert.Equal(65, s.Gruppen[1].X0);
        // die Steigknoten der Bündelstränge verschränkt (Stufe 3.5): Platz 0 auf y0 + 20, Platz 1 auf + 3.5, Platz 2 auf + 7
        var pw = Gruppe(s, "PW 1");
        Assert.Equal([70.0, 73.5, 77.0], pw.Spalten.Select(sp => Lage(s, $"T-{sp.Strang!.Kennzeichen}-E").Y));
    }

    [Fact]
    public void Gruppen_OhneSteigzone_NachLageImGebaeude_NichtNachKennzeichen()
    {
        // Kennzeichen absichtlich gegen die Lage: S-A liegt im Osten, S-B im Westen; S-0 hat keine Bauteile (nicht platziert)
        var p = Steigzonen(("S-A", "KW", null, 8000, 0), ("S-B", "KW", null, 2000, 0), ("S-C", "WW", null, 5000, 0), ("S-0", "KW", null, null, 0));
        var s = Plan(p, An);
        Assert.Equal(["S-B", "S-C", "S-A", "S-0"], s.Gruppen.Select(g => g.Bezeichnung));   // ohne Lage am Schluss
        Assert.Equal([30.0, 65.0, 100.0, 135.0], s.Gruppen.Select(g => g.X0));
        Assert.Null(Gruppe(s, "S-0").Lage);
        Assert.Contains(s.Hinweise, h => h.StartsWith("Ohne Lage im Gebäude") && h.Contains("S-0"));
        Blick(p, "nord");
        Assert.Equal(["S-A", "S-C", "S-B", "S-0"], Plan(p, An).Gruppen.Select(g => g.Bezeichnung));
        // die Anordnung wird nicht gespeichert: nur die Blickrichtung steht an der Ansicht
        Assert.Null(Hole(p, An).Eigenschaft("lagen"));
    }

    // ------------------------------------------------------------------ Endgeräte nach Netzreihenfolge

    /// <summary>Strang KW-1: Steig-T TS-0 (UG) – TS-1 (EG, x 1000); Etagenleitung TS-1 → T-E1 (1500) → T-E2 (2500) → T-E3 (3500) →
    /// WT-1 (4500); Abzweige T-E1.c → WT-4, T-E2.c → WT-3, T-E3.c → WT-2 (je 500 mm). Kennzeichen absichtlich gegen die Reihenfolge.
    /// Platziert (Längen in mm): Abzweige 500 / 1500 / 2500, WT-2 3000 < WT-1 3500 → WT-4, WT-3, WT-2, WT-1.
    /// Nicht platziert (nur Schritte): WT-2 und WT-1 hängen am selben T mit gleich vielen Schritten → nach Kennzeichen WT-1 vor WT-2.</summary>
    [Theory]
    [InlineData(true, "WT-4,WT-3,WT-2,WT-1")]
    [InlineData(false, "WT-4,WT-3,WT-1,WT-2")]
    public void Endgeraete_NachNetzreihenfolge_KennzeichenUmgekehrt(bool platziert, string reihe)
    {
        var p = Steigzonen();
        Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Strang"), ("kennzeichen", "KW-1"), ("teilVon", "KW"), ("eigenschaften", new JsonObject { ["art"] = "steigstrang" }))));
        JsonObject Punkt(double x, double y, double z) => new() { ["art"] = "punkt", ["punkte"] = Pts([x, y, z]) };
        void Anlegen(string klasse, string kz, string geschoss, double x, double y, double z, string? teilVon = "KW-1")
        {
            var o = Par(("klasse", klasse), ("kennzeichen", kz), ("geschoss", geschoss));
            if (teilVon is not null) o["teilVon"] = teilVon;
            if (platziert) o["geometrie"] = Punkt(x, y, z);
            Ok(p.Ausfuehren("element_anlegen", o));
        }
        void Rohr(string kz, string von, string nach, params double[][] punkte)
        {
            if (platziert)
                Ok(p.Ausfuehren("leitung_ziehen", Par(("kennzeichen", kz), ("strang", "KW-1"), ("dn", 15), ("von", von), ("nach", nach), ("punkte", Pts(punkte)))));
            else
            {
                Ok(p.Ausfuehren("element_anlegen", Par(("klasse", "Rohr"), ("kennzeichen", kz), ("teilVon", "KW-1"), ("geschoss", punkte[0][2] < 0 ? "UG" : "EG"))));
                Ok(p.Ausfuehren("verbinden", Par(("von", von), ("nach", kz + ".anfang"))));
                Ok(p.Ausfuehren("verbinden", Par(("von", kz + ".ende"), ("nach", nach))));
            }
        }
        Anlegen("TStueck", "TS-0", "UG", 1000, 0, -2400);
        Anlegen("TStueck", "TS-1", "EG", 1000, 0, 300);
        for (int i = 1; i <= 3; i++) Anlegen("TStueck", $"T-E{i}", "EG", 500 + 1000 * i, 0, 300);
        Anlegen("Waschtisch", "WT-4", "EG", 1500, 500, 0, null);
        Anlegen("Waschtisch", "WT-3", "EG", 2500, 500, 0, null);
        Anlegen("Waschtisch", "WT-2", "EG", 3500, 500, 0, null);
        Anlegen("Waschtisch", "WT-1", "EG", 4500, 0, 0, null);
        Rohr("R-0", "TS-0.b", "TS-1.a", [1000, 0, -2400], [1000, 0, 300]);
        Rohr("R-1", "TS-1.c", "T-E1.a", [1000, 0, 300], [1500, 0, 300]);
        Rohr("R-2", "T-E1.b", "T-E2.a", [1500, 0, 300], [2500, 0, 300]);
        Rohr("R-3", "T-E2.b", "T-E3.a", [2500, 0, 300], [3500, 0, 300]);
        Rohr("R-W4", "T-E1.c", "WT-4.kw", [1500, 0, 300], [1500, 500, 300]);
        Rohr("R-W3", "T-E2.c", "WT-3.kw", [2500, 0, 300], [2500, 500, 300]);
        Rohr("R-W2", "T-E3.c", "WT-2.kw", [3500, 0, 300], [3500, 500, 300]);
        Rohr("R-W1", "T-E3.b", "WT-1.kw", [3500, 0, 300], [4500, 0, 300]);
        var s = Plan(p, An);
        // Gruppe KW-1: x0 30, Stranglinie 40; Endgeräte 55, 68, 81, 94 auf dem Boden der Zeile EG (Nachtrag B8d): Etagenleitung TS-1
        // (70) + 3 = Unterkante 73, Waschtisch-Knoten 2.625 darüber = 75.625 (bis B8c 50 + 42 = 92)
        var namen = reihe.Split(',');
        for (int i = 0; i < namen.Length; i++) Assert.Equal((55.0 + 13 * i, 75.625), Lage(s, namen[i]));
        Assert.Equal((40.0, 70.0), Lage(s, "TS-1"));
        Assert.Equal("senkrecht", Knoten(s, "TS-1").Ausrichtung);
        if (!platziert) return;
        // Etagenknoten auf der Etagenleitung (y des Steigknotens) unter dem ersten Endgerät dahinter
        Assert.Equal((55.0, 70.0), Lage(s, "T-E1"));
        Assert.Equal((68.0, 70.0), Lage(s, "T-E2"));
        Assert.Equal((81.0, 70.0), Lage(s, "T-E3"));
        Assert.All(new[] { "T-E1", "T-E2", "T-E3" }, kz => Assert.Equal("etage", Knoten(s, kz).Ausrichtung));
        Assert.Equal("(40,70) (55,70)", Zug(LinieMit(s, "R-1")));
        Assert.Equal("(55,70) (55,75.63)", Zug(LinieMit(s, "R-W4")));   // kurzer Anschluss senkrecht nach unten (B8d)
        Assert.Equal("(81,70) (94,70) (94,75.63)", Zug(LinieMit(s, "R-W1")));   // der Etagenleitung entlang, dann hinauf
        Assert.Equal(70, Gruppe(s, "KW-1").Breite);                            // Bedarf 10 + 15 + 3 · 13 + 5 = 69
    }

    [Fact]
    public void Endgeraete_Abwasser_InNetzreihenfolgeAnDerEtagenleitung()
    {
        var p = Mfh();
        var s = Plan(p, Abwasser);
        // Gruppen nach x: WAS-H 01 (2300), WAR-R 01 (6000), SW-S (Mittel der Sammelleitung), RW-G, WAS-H 02 (9300)
        Assert.Equal(["WAS-H 01", "WAR-R 01", "SW-S", "RW-G", "WAS-H 02"], s.Gruppen.Select(g => g.Bezeichnung));
        // vom Fallstrang weg: WC (1. T), WT (2. T), am 3. T Dusche (1900 mm) vor Spültisch (2200 mm)
        foreach (var (g, i) in new[] { ("E", 1), ("1", 2), ("2", 3) })
        {
            double y0 = 50 * i;
            Assert.Equal([55.0, 68.0, 81.0, 94.0], new[] { "WC", "WT", "DU", "SP" }.Select(a => Lage(s, $"{a}-{g}1").X));
            // Nachtrag B8d: auf dem Boden – Unterkante = Etagenleitung (y0 + 20) + 3, Knoten um den Abfluss des Sinnbilds darüber
            Assert.All(new[] { "WC", "WT", "DU", "SP" }, a => Assert.Equal(y0 + 23, Knoten(s, $"{a}-{g}1").Unterkante));
            Assert.Equal([y0 + 27.375, y0 + 25.625, y0 + 25.5, y0 + 24.575], new[] { "WC", "WT", "DU", "SP" }.Select(a => Lage(s, $"{a}-{g}1").Y));
            // Etagen-T auf der Etagenleitung (y des Steig-T y0 + 20) unter dem ersten Endgerät dahinter
            Assert.Equal((55.0, y0 + 20), Lage(s, $"TS-S{g}1-1"));
            Assert.Equal((68.0, y0 + 20), Lage(s, $"TS-S{g}1-2"));
            Assert.Equal((81.0, y0 + 20), Lage(s, $"TS-S{g}1-3"));
        }
        // offenes Ende der Lüftung über dem obersten Fallstrang-T: auf dem Fallstrang ist a oben
        var lueftung = LinieMit(s, "SW-1-L");
        Assert.Equal("offen", lueftung.Von.Offen);
        Assert.Equal("(40,178) (40,170)", Zug(lueftung));
    }

    // ------------------------------------------------------------------ Wohnungsblöcke

    /// <summary>MFH, Schema Wasser, Gruppe PW 1 (x0 30; Stranglinien KW-1 40, WW-1 43, ZIR-1 46), EG (y0 50). Steigknoten: KW-1 TS-K1-E
    /// (Stufe 0 → Ebene 0), TS-K1-EA (Stufe 1 → Ebene 3), WW-1 TS-W1-E (Ebene 1); belegt 0, 1, 3 → y 70, 73.5, 77. Plätze: Ausgussbecken
    /// AB-E1 (Wurzel TS-K1-EA oben) zuerst bei 46 + 15 = 61, dann der Block «Wohnung 1» mit zwei Armaturplätzen: Eintritt 61 + 13 = 74,
    /// Armaturen bei 81 und 88, Blockrand links 74 + 3 · 7 − 3 = 92, rechts 92 + 38 = 130, unten 50 + 17 = 67, Höhe 50 − 17 − 4 = 29.</summary>
    [Fact]
    public void Wohnungsblock_TabelleUndZaehlerAmAnschluss()
    {
        var p = Mfh();
        var s = Plan(p, Wasser);
        Assert.True(s.Wohnungsbloecke);
        Assert.Equal(6, s.Bloecke.Count);
        var b = s.Bloecke.Single(x => x.Zone.Kennzeichen == "W-E1");
        Assert.Equal("Wohnung 1, 3.5 Z.", b.Titel);
        Assert.Equal("PW 1", b.Gruppe.Bezeichnung);
        Assert.Equal("EG", b.Zeile.Titel);
        // Tabelle je Apparateart in Netzreihenfolge (KW: WC, WT, SP, DU), Stückzahl; LU, KW, WW leer bis Modul 4
        Assert.Equal(["WC 1", "WT 1", "SP 1", "DU 1"], b.Zeilen.Select(z => $"{z.Kuerzel} {z.Stueck}"));
        Assert.Equal(4, b.Stueck);
        Assert.All(b.Zeilen, z => Assert.True(z.Lu is null && z.Kw is null && z.Ww is null));
        Assert.Null(b.Lu);
        // Lage
        Assert.Equal((92.0, 67.0, 38.0, 29.0), (b.X, b.Y, b.Breite, b.Hoehe));
        Assert.Equal(74, b.EintrittX);
        Assert.Equal((70.0, 73.5, 77.0), (Lage(s, "TS-K1-E").Y, Lage(s, "TS-W1-E").Y, Lage(s, "TS-K1-EA").Y));
        // Absperrventil und Zähler am Anschluss: je Strang eine Anschlusslinie auf der Höhe ihres Steig-T
        Assert.Equal(2, b.Armaturplaetze);
        var kw = b.Anschluesse.Single(a => a.Strang!.Kennzeichen == "KW-1");
        var ww = b.Anschluesse.Single(a => a.Strang!.Kennzeichen == "WW-1");
        Assert.Equal(["AV-KE1", "WZ-KE1"], kw.Armaturen.Select(k => k.Kennzeichen));
        Assert.Equal(["AV-WE1", "WZ-WE1"], ww.Armaturen.Select(k => k.Kennzeichen));
        Assert.Equal((70.0, 73.5), (kw.Y, ww.Y));
        Assert.Equal((81.0, 70.0), Lage(s, "AV-KE1"));
        Assert.Equal((88.0, 70.0), Lage(s, "WZ-KE1"));
        Assert.Equal((81.0, 73.5), Lage(s, "AV-WE1"));
        Assert.Equal((88.0, 73.5), Lage(s, "WZ-WE1"));
        Assert.All(new[] { "AV-KE1", "WZ-KE1" }, kz => { Assert.Equal("etage", Knoten(s, kz).Ausrichtung); Assert.False(Knoten(s, kz).Verdeckt); });
        Assert.Equal("zaehler", SchemaSymbole.KlassenSchluessel(Kat, Hole(p, "WZ-KE1")));
        // Linien: Steig-T → Ventil → Zähler → Blockrand, gerade auf der Anschlusslinie
        Assert.Equal("(40,70) (81,70)", Zug(LinieMit(s, "KW-E1-0")));
        Assert.Equal("(88,70) (92,70)", Zug(LinieMit(s, "KW-E1-2")));
        Assert.Equal("(88,73.5) (92,73.5)", Zug(LinieMit(s, "WW-E1-2")));
        // Endgeräte und T-Stücke der Wohnung sind verdeckt; ihre Linien haben im Block die Länge 0
        Assert.All(new[] { "WC-E1", "WT-E1", "SP-E1", "DU-E1", "TS-KE1-1", "TS-KE1-2", "TS-KE1-3", "TS-WE1-1", "TS-WE1-2" },
            kz => Assert.True(Knoten(s, kz).Verdeckt && Knoten(s, kz).Block == b, kz));
        foreach (var kz in new[] { "KW-E1-WC", "KW-E1-3", "KW-E1-DU", "WW-E1-WT", "WW-E1-DU" })
        {
            var l = LinieMit(s, kz);
            Assert.True(l.Punkte.All(q => Math.Abs(q[0] - 92) < 1e-9 && Math.Abs(q[1] - l.Punkte[0][1]) < 1e-9), $"{kz}: {Zug(l)}");
        }
        Assert.Equal(73.5, LinieMit(s, "WW-E1-WT").Punkte[0][1]);   // Warmwasser im Block auf der WW-Anschlusslinie
        // Grafik: Rechteck mit Titel, Kopfzeile, Zeilen und Total; keine Spalten LU/KW/WW; Armaturen ohne Kennzeichen
        var g = Grafik.Plan(p, Wasser);
        var t = Assert.Single(g.Teile, x => x.Id == b.Zone.Id);
        Assert.Contains(t.Primitive, x => x.Art == "polygon" && x.Stil.Fuellung == "papier");
        var texte = t.Primitive.Where(x => x.Art == "text").Select(x => x.Text).ToList();
        Assert.Equal(["Wohnung 1, 3.5 Z.", "App.", "Stk.", "WC", "1", "WT", "1", "SP", "1", "DU", "1", "Total", "4"], texte);
        Assert.DoesNotContain(texte, x => x is "LU" or "KW" or "WW");
        Assert.DoesNotContain(g.Teile.Single(x => x.Id == Hole(p, "WZ-KE1").Id).Primitive, x => x.Art == "text" && x.Text == "WZ-KE1");
        var wc = g.Teile.Single(x => x.Id == Hole(p, "WC-E1").Id);
        Assert.All(wc.Primitive, x => Assert.True(x.Stil.NurBildschirm));     // verdeckt: auf dem Plan nichts, am Bildschirm anklickbar
    }

    [Fact]
    public void Wohnungsblock_EndgeraetOhneZone_BleibtEinzeln()
    {
        var p = Mfh();
        var s = Plan(p, Wasser);
        var ab = Knoten(s, "AB-E1");                                 // Ausgussbecken ohne Raum, also ohne Zone
        Assert.Null(ab.Block);
        Assert.False(ab.Verdeckt);
        // vor dem Block (Wurzel TS-K1-EA liegt über TS-K1-E); Nachtrag B8d auf dem Boden der Zeile EG: oberste Etagenleitung TS-K1-EA
        // (77) + 3 = 80, Waschtrog-Knoten 3 darüber
        Assert.Equal((61.0, 83.0), (ab.X, ab.Y));
        // Linien der Bündelstränge enden am Endgerät nebeneinander: KW-1 hat Platz 0 von 3 → (0 − 1) · 1.5 = −1.5
        Assert.Equal("(40,77) (59.5,77) (59.5,83)", Zug(LinieMit(s, "KW-E1-AB")));
        Assert.Contains(Grafik.Plan(p, Wasser).Teile.Single(x => x.Id == ab.Id).Primitive, x => x.Text == "AB-E1" && !x.Stil.NurBildschirm);   // Kennzeichen am Sinnbild (Waschtrog)
        // ohne wohnungsbloecke steht jedes Endgerät einzeln
        Ok(p.Ausfuehren("element_aendern", Par(("element", Wasser), ("eigenschaften", new JsonObject { ["wohnungsbloecke"] = false }))));
        var ohne = Plan(p, Wasser);
        Assert.Empty(ohne.Bloecke);
        Assert.DoesNotContain(ohne.Graph.Knoten, k => k.Verdeckt);
        Assert.Equal([61.0, 74.0, 87.0, 100.0, 113.0], new[] { "AB-E1", "WC-E1", "WT-E1", "SP-E1", "DU-E1" }.Select(kz => Lage(ohne, kz).X));
    }

    [Fact]
    public void Wohnungsblock_EndgeraetAnZweiStraengen_StehtImBuendelSeinerSteigzone()
    {
        var s = Plan(Mfh(), Wasser);
        foreach (var kz in new[] { "WT-E1", "DU-11", "SP-21" })
            Assert.Equal("PW 1", Knoten(s, kz).Spalte!.Gruppe.Steigzone);
        foreach (var kz in new[] { "WT-E2", "DU-12", "SP-22" })
            Assert.Equal("PW 2", Knoten(s, kz).Spalte!.Gruppe.Steigzone);
    }

    // ------------------------------------------------------------------ Geschosse, Kreise, Dämmung

    [Fact]
    public void Deckenband_UndGeschossnameInnerhalbDesSchemas()
    {
        var p = Mfh();
        var s = Plan(p, Wasser);
        var g = Grafik.Plan(p, Wasser);
        double rechts = s.Gruppen[^1].X0 + s.Gruppen[^1].Breite;
        // je Geschoss ein Deckenband auf y0 (± 1.2) über die ganze Breite, dazu eines über dem obersten Geschoss (y 200)
        foreach (var (kz, y0) in new[] { ("UG", 0.0), ("EG", 50.0), ("1.OG", 100.0), ("2.OG", 150.0) })
        {
            var t = Assert.Single(g.Teile, x => x.Id == Hole(p, kz).Id);
            var band = Assert.Single(t.Primitive, x => x.Art == "polygon");
            Assert.Equal("beton", band.Stil.Muster);
            Nahe([0, y0 - 1.2, rechts, y0 + 1.2], Box(band.Punkte), 1e-9);
            // Name links innerhalb des Rahmens (x 2, Rand links 30), in der Mitte der Zeile
            var texte = t.Primitive.Where(x => x.Art == "text").ToList();
            Assert.Equal(kz, texte[0].Text);
            Assert.All(texte, x => Assert.True(x.Punkte[0][0] >= 0 && x.Punkte[0][0] < SchemaRaster.RandLinks && x.Punkte[0][1] > y0 + 2 && x.Punkte[0][1] < y0 + 48));
        }
        Assert.Contains(g.Teile, x => x.Id is null && x.Primitive.Any(q => q.Stil.Muster == "beton" && Math.Abs(Box(q.Punkte)[1] - 198.8) < 1e-9));
        Assert.DoesNotContain(g.Teile.SelectMany(x => x.Primitive), x => x.Stil.Linie == "strichpunkt");
        // die Begrenzung umfasst Rahmen und Kopf (Kreise): links 0, oben über den Kreisen
        Assert.Equal(0, g.Begrenzung![0]);
        Assert.True(g.Begrenzung[2] >= rechts);
        Assert.True(g.Begrenzung[3] >= s.Gruppen.Where(x => x.MitKreis).Max(x => x.KreisY + x.KreisRadius));
        // auf A2 platziert: Fenster samt Titel innerhalb des Blatts, Geschossnamen und Titel stehen im PDF
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "B8-A2"), ("titel", "Schema Wasser"), ("format", "A2"))));
        var e = Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", Wasser))));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("kein freier Platz"));
        var lage = p.Modell.DerKlasse("Ansichtsfenster").Single().Geometrie!.Punkte[0];
        double breite = g.Begrenzung[2] - g.Begrenzung[0] + 2, hoehe = g.Begrenzung[3] - g.Begrenzung[1] + 2;
        Assert.InRange(lage[0] - breite / 2, Blattformate.Rand, 594 - Blattformate.Rand);
        Assert.InRange(lage[0] + breite / 2, Blattformate.Rand, 594 - Blattformate.Rand);
        Assert.InRange(lage[1] - hoehe / 2 - Blattformate.TitelHoehe, Blattformate.Rand, 420 - Blattformate.Rand);
        var pdf = System.Text.Encoding.Latin1.GetString(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        foreach (var text in new[] { "(2.OG)", "(Erdgeschoss)", "(Schema Wasser)", "(PW 1)", "(Wohnung 1, 3.5 Z.)" })
            Assert.Contains(text, pdf);
    }

    [Fact]
    public void Kreis_MitBezeichnung_SteigzoneSonstStrang()
    {
        var p = Mfh();
        var w = Plan(p, Wasser);
        // Wasser: oben = 200; Kreis «PW 1» r 4.5 (Text 0.6 · 2.2 · 4 / 2 + 1.2 = 3.84 < 4.5), Mitte über dem Bündel (40 + 43 + 46) / 3
        var pw1 = Gruppe(w, "PW 1");
        Assert.True(pw1.MitKreis);
        Assert.Equal((43.0, 207.7, 4.5), (pw1.KreisX, pw1.KreisY, pw1.KreisRadius));   // 200 + 1.2 + 2 + 4.5
        Assert.Equal(["PW 1", "PW 2"], w.Gruppen.Where(x => x.MitKreis).Select(x => x.Bezeichnung));
        Assert.False(Gruppe(w, "KW-V").MitKreis);                                         // Verteilleitung: Titel, kein Kreis
        var g = Grafik.Plan(p, Wasser);
        Assert.Contains(g.Teile, t => t.Primitive.Any(x => x.Art == "kreis" && x.Radius == 4.5 && Math.Abs(x.Punkte[0][0] - 43) < 1e-9)
                                      && t.Primitive.Any(x => x.Text == "PW 1" && x.Ausrichtung == "mitte"));
        // Abwasser: «WAS-H 01» (8 Zeichen) r = 0.6 · 2.2 · 8 / 2 + 1.2 = 6.48; alle Kreise auf derselben Höhe 200 + 3.2 + 6.48
        var a = Plan(p, Abwasser);
        Assert.Equal(["WAS-H 01", "WAR-R 01", "WAS-H 02"], a.Gruppen.Where(x => x.MitKreis).Select(x => x.Bezeichnung));
        Assert.All(a.Gruppen.Where(x => x.MitKreis), x => { Assert.Equal(6.48, x.KreisRadius, 6); Assert.Equal(209.68, x.KreisY, 6); });
        // ohne Steigzone: Kennzeichen des Strangs (Testhaus, Strang KW-1), Name klein daneben
        Assert.Equal("KW-1", Plan(Testhaus()).Gruppen[0].Bezeichnung);
    }

    [Fact]
    public void Daemmung_JeGeschossUndGruppe_GleicheWerteZusammengefasst()
    {
        var p = Mfh();
        var s = Plan(p, Wasser);
        // PW 1, EG: KW-1 und WW-1 50 mm, ZIR-1 30 mm → zwei Texte; der kleinere Wert oben auf y0 + 15, der nächste 3 darunter
        var eg = s.Daemmungen.Where(d => d.Gruppe.Bezeichnung == "PW 1" && d.Zeile.Titel == "EG").OrderBy(d => d.Dicke).ToList();
        Assert.Equal(["Dämmung 30 mm", "Dämmung 50 mm"], eg.Select(d => d.Text));
        Assert.Equal(["ZIR-1"], eg[0].Spalten.Select(sp => sp.Strang!.Kennzeichen));
        Assert.Equal(["KW-1", "WW-1"], eg[1].Spalten.Select(sp => sp.Strang!.Kennzeichen));
        Assert.Equal((65.0, 62.0), (eg[0].Y, eg[1].Y));
        Assert.Equal((38.5, 47.5), (eg[1].X0, eg[1].XText));                  // Strich ab der ersten Stranglinie − 1.5, Text rechts des Bündels
        // 2.OG: nur die Zirkulation ist dort gedämmt (die KW/WW-Leitungen ins 2.OG zählen zum 1.OG, ihrem ersten Punkt)
        Assert.Equal(["Dämmung 30 mm"], s.Daemmungen.Where(d => d.Gruppe.Bezeichnung == "PW 1" && d.Zeile.Titel == "2.OG").Select(d => d.Text));
        // nur wenn gesetzt: das Abwasser ist nicht gedämmt
        Assert.Empty(Plan(p, Abwasser).Daemmungen);
        // Grafik: Strich mit Punkten auf den Stranglinien und Text
        var g = Grafik.Plan(p, Wasser);
        var t = g.Teile.Single(x => x.Primitive.Any(q => q.Text == "Dämmung 50 mm" && Math.Abs(q.Punkte[0][1] - 62.6) < 1e-9 && Math.Abs(q.Punkte[0][0] - 47.5) < 1e-9));
        Assert.Equal([40.0, 43.0], t.Primitive.Where(q => q.Art == "kreis").Select(q => q.Punkte[0][0]));
        Assert.DoesNotContain(g.Teile.SelectMany(x => x.Primitive), q => q.Text?.Contains('(') == true && q.Text.StartsWith("NW"));   // keine Belastung in Klammern
    }

    // ------------------------------------------------------------------ Abfrage, Determinismus

    [Fact]
    public void Abfrage_Schema_GruppenBloeckeDaemmung()
    {
        var p = Mfh();
        var o = (JsonObject)Tuer.Abfrage(p, "schema", Par(("ansicht", Wasser)));
        Assert.Equal("sued", o["blickrichtung"]!.GetValue<string>());
        Assert.True(o["wohnungsbloecke"]!.GetValue<bool>());
        var pw1 = o["gruppen"]!.AsArray().Single(x => x!["bezeichnung"]!.GetValue<string>() == "PW 1")!;
        Assert.Equal(["KW-1", "WW-1", "ZIR-1"], pw1["straenge"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(4.5, pw1["kreis"]!["radius"]!.GetValue<double>());
        var block = o["bloecke"]!.AsArray().Single(x => x!["kennzeichen"]!.GetValue<string>() == "W-E1")!;
        Assert.Equal("Wohnung 1, 3.5 Z.", block["titel"]!.GetValue<string>());
        Assert.Null(block["zeilen"]![0]!["lu"]);
        Assert.Equal(["app", "stk", "lu", "kw", "ww"], block["spalten"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()));
        Assert.Equal([true, true, false, false, false], block["spalten"]!.AsArray().Select(x => x!["gezeigt"]!.GetValue<bool>()));
        Assert.Equal(4, block["total"]!["stueck"]!.GetValue<int>());
        Assert.Contains(o["daemmung"]!.AsArray(), x => x!["text"]!.GetValue<string>() == "Dämmung 50 mm");
        var wc = o["knoten"]!.AsArray().Single(x => x!["kennzeichen"]!.GetValue<string>() == "WC-E1")!;
        Assert.Equal("W-E1", wc["block"]!.GetValue<string>());
        Assert.True(wc["verdeckt"]!.GetValue<bool>());
        var spalte = o["spalten"]!.AsArray().Single(x => x!["kennzeichen"]?.GetValue<string>() == "WW-1")!;
        Assert.Equal("PW 1", spalte["steigzone"]!.GetValue<string>());
        Assert.Equal(43, spalte["linie"]!.GetValue<double>());
    }

    [Fact]
    public void Determinismus_Mfh_GleicheLagenAuchMitVertauschterSpeicherreihenfolge()
    {
        var p = Mfh();
        var wasser = Plan(p, Wasser).AlsJson().ToJsonString();
        var abwasser = Json.Text(Grafik.Plan(p, Abwasser));
        Assert.Equal(wasser, Plan(p, Wasser).AlsJson().ToJsonString());
        var abbild = p.Modell.Abbild([]);
        abbild.Elemente.Reverse();
        abbild.Verbindungen.Reverse();
        p.Modell.Laden(abbild);
        Assert.Equal(wasser, Plan(p, Wasser).AlsJson().ToJsonString());
        Assert.Equal(abwasser, Json.Text(Grafik.Plan(p, Abwasser)));
    }

    [Fact]
    public void HandLage_GiltAuchImBlock()
    {
        var p = Mfh();
        Ok(p.Ausfuehren("schema_lage", Par(("ansicht", Wasser), ("element", "WZ-KE1"), ("x", 85), ("y", 60))));
        var s = Plan(p, Wasser);
        var wz = Knoten(s, "WZ-KE1");
        Assert.True(wz.Manuell);
        Assert.Equal((85.0, 60.0, "frei"), (wz.X, wz.Y, wz.Ausrichtung));
        Assert.Equal((92.0, 67.0), (s.Bloecke.Single(b => b.Zone.Kennzeichen == "W-E1").X, s.Bloecke.Single(b => b.Zone.Kennzeichen == "W-E1").Y));
        // die Linie vom Zähler in den Block wird neu geführt: von Hand gesetzt gilt der Zähler als frei, das Ende im Block bleibt auf
        // der KW-Anschlusslinie (y 70) am Blockrand
        Assert.Equal("(85,60) (92,60) (92,70)", Zug(LinieMit(s, "KW-E1-2")));
    }

    // ------------------------------------------------------------------ Bild (Sichtprüfung)

    [Fact]
    public void Bild_MfhWasserUndAbwasser_AlsPlanPdf()
    {
        var p = Mfh();
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "B8-1"), ("titel", "Schema Wasser (fiktiv)"), ("format", "A2"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-1"), ("ansicht", Wasser))));
        Ok(p.Ausfuehren("planblatt_anlegen", Par(("nummer", "B8-2"), ("titel", "Schema Abwasser (fiktiv)"), ("format", "A2"))));
        Ok(p.Ausfuehren("ansicht_platzieren", Par(("planblatt", "PB-2"), ("ansicht", Abwasser))));
        foreach (var (blatt, name) in new[] { ("PB-1", "b8_schema_wasser"), ("PB-2", "b8_schema_abwasser") })
        {
            var pdf = Planausgabe.Pdf(p, blatt);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
            if (Environment.GetEnvironmentVariable("HLKS_SCHEMA_BILD") is { Length: > 0 } ordner)   // Sichtprüfung von Hand
                File.WriteAllBytes(Path.Combine(ordner, name + ".pdf"), pdf);
        }
        foreach (var h in Plan(p, Wasser).Hinweise) aus.WriteLine("Wasser: " + h);
        foreach (var h in Plan(p, Abwasser).Hinweise) aus.WriteLine("Abwasser: " + h);
    }
}
