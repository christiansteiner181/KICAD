using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SchemaStrangschemaHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3, Nachtrag B8b: Apparate als Sinnbilder im Strangschema (Geometrie der HKAP-Symbolbibliothek, Testkopie
/// 19.10.2026) und Freistellen der Grössentexte auf gleicher Spur. Alle Werte FIKTIV bzw. Darstellungsannahmen.
///
/// Handrechnung des Anschlusspunkts (Abfluss) je Sinnbild: Knoten = Mitte der Höhe der HKAP-Geometrie (y nach unten), Abfluss
/// = Ay; Abstand unter dem Knoten = (Ay − Mitte) · Skala (HKAP y nach unten, also im Schema nach unten):
///   WC: y −22.5 … −5, Mitte −13.75, Ay −5 → 8.75 · 0.5 = 4.375
///   Waschtisch (Funnel): y −12.5 … −5, Mitte −8.75, Ay −5 → 3.75 · 0.7 = 2.625
///   Dusche: y −12.5 … −2.5, Mitte −7.5, Ay −2.5 → 5 · 0.5 = 2.5
///   Badewanne: y −6.25 … −2.5, Mitte −4.375, Ay −2.5 → 1.875 · 1.2 = 2.25
///   Spültisch: y −7 … −2.5, Mitte −4.75, Ay −2.5 → 2.25 · 0.7 = 1.575
///   Waschtrog (Ausguss): y −7.5 … −2.5, Mitte −5, Ay −2.5 → 2.5 · 1.2 = 3.0
///   Waschmaschine: y −5 … 5, Mitte 0, Ay 5 → 5 · 0.6 = 3.0
///   Bodenablauf: y −4 … 4, Mitte 0, Ay 4 → 4 · 0.75 = 3.0
///   Dachablauf: y −4 (Kreis r 1 um −3) … 3, Mitte −0.5, Ay 3 → 3.5 · 0.7 = 2.45
///   Kontrollschacht: y −5 … 5, Mitte 0, Ay 0 → 0 (Mitte).</summary>
public class SchemaSinnbilderTests(ITestOutputHelper aus)
{
    static Projekt Leer() => Projekt.Anlegen(Kat, null, "Test Sinnbilder (fiktiv)", "T", "pc");

    static Element Anlegen(Projekt p, string klasse, string kz)
    {
        var o = new JsonObject { ["klasse"] = klasse, ["kennzeichen"] = kz };
        if (klasse == "Kontrollschacht")   // braucht ein System
        {
            if (p.Modell.Finde("SW") is null)
                Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "System", ["kennzeichen"] = "SW", ["eigenschaften"] = new JsonObject { ["systemtyp"] = "Schmutzwasser" } }));
            o["system"] = "SW";
        }
        Ok(p.Ausfuehren("element_anlegen", o));
        return Hole(p, kz);
    }

    public static IEnumerable<object[]> Sinnbilder() =>
    [
        ["WC", 0.0, -4.375, 2.5, 4.375],
        ["Waschtisch", 0.0, -2.625, 3.5, 2.625],
        ["Dusche", 0.0, -2.5, 6.25, 2.5],
        ["Badewanne", 0.0, -2.25, 6.0, 2.25],
        ["Spueltisch", 0.0, -1.575, 7.0, 1.575],
        ["Ausgussbecken", 0.0, -3.0, 3.0, 3.0],
        ["Waschmaschine", 0.0, -3.0, 3.0, 3.0],
        ["Bodenablauf", 0.0, -3.0, 3.0, 3.0],
        ["Dachablauf", 0.0, -2.45, 3.5, 2.45],
        // Kontrollschacht: das HKAP-Draufsichtsymbol entfällt im Strangschema (Nachtrag B8d, Schacht im Schnitt; SchemaUntergrundTests)
    ];

    [Theory]
    [MemberData(nameof(Sinnbilder))]
    public void Sinnbild_PrimitiveUndAnschlusspunkt(string klasse, double ax, double ay, double halbX, double halbY)
    {
        var p = Leer();
        var e = Anlegen(p, klasse, "X-1");
        Assert.Equal("sinnbild", SchemaSymbole.Schluessel(p, e));
        // Anschlusspunkt: x auf der Abflussachse, y wie Handrechnung unter dem Knoten (100, 50)
        var an = SchemaSymbole.SinnbildAnschluss(p.Katalog, e, 100, 50);
        Assert.NotNull(an);
        Assert.Equal(100 + ax, an!.Value.X, 3);
        Assert.Equal(50 + ay, an.Value.Y, 3);
        // Primitive um den Knoten, keine Kürzel-Texte, Kästchen entfällt
        var (prims, hx, hy) = SchemaSymbole.Zeichnen(p, e, 100, 50, false, "#8b5a2b", "mittel");
        Assert.NotEmpty(prims);
        Assert.DoesNotContain(prims, x => x.Art == "text");
        Assert.All(prims, x => Assert.Equal(Stifte.Duenn, x.Stil.Stift));
        Assert.Equal(halbX, hx, 3);
        Assert.Equal(halbY, hy, 3);
        // der Abfluss liegt auf der Unterkante der Primitive bzw. dem Stiel: kein Primitiv reicht tiefer als der Anschlusspunkt
        var b = Grafik.Begrenzung([new Grafikteil { Primitive = prims }])!;
        Assert.Equal(an.Value.Y, b[1], 3);
    }

    [Theory]
    [InlineData("WC")]
    [InlineData("Waschtisch")]
    [InlineData("Dusche")]
    [InlineData("Badewanne")]
    [InlineData("Spueltisch")]
    [InlineData("Ausgussbecken")]
    [InlineData("Waschmaschine")]
    public void Sinnbild_Entnahmestellen_InDerApparatefarbe(string klasse)
    {
        var p = Leer();
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, Anlegen(p, klasse, "X-1"), 0, 0, true, "#8b5a2b", "mittel");   // senkrecht: Sinnbilder bleiben aufrecht
        Assert.All(prims, x => Assert.Equal(SchemaSymbole.ApparateFarbe, x.Stil.Farbe));
    }

    [Theory]
    [InlineData("Bodenablauf")]
    [InlineData("Dachablauf")]
    [InlineData("Kontrollschacht")]
    public void Sinnbild_AblaeufeUndSchaechte_InDerMedienfarbe(string klasse)
    {
        var p = Leer();
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, Anlegen(p, klasse, "X-1"), 0, 0, false, "#8b5a2b", "mittel");
        Assert.All(prims, x => Assert.Equal("#8b5a2b", x.Stil.Farbe));
    }

    [Fact]
    public void Sinnbild_WC_GespiegeltUndSkaliert()
    {
        // HKAP: Spülkasten y −22.5 … −12.5 (oben), Trichter −10 … −5 (unten); KICAD y nach oben: Kasten oben, Spitze unten
        var p = Leer();
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, Anlegen(p, "WC", "WC-1"), 0, 0, false, null, "mittel");
        var kasten = prims.Single(x => x.Art == "polygon" && x.Punkte.Count == 4);
        Assert.Equal(4.375, kasten.Punkte.Max(q => q[1]), 3);       // −(−22.5 + 13.75) · 0.5
        Assert.Equal(-0.625, kasten.Punkte.Min(q => q[1]), 3);      // −(−12.5 + 13.75) · 0.5
        var trichter = prims.Single(x => x.Art == "polygon" && x.Punkte.Count == 3);
        Assert.Contains(trichter.Punkte, q => Math.Abs(q[0]) < 1e-9 && Math.Abs(q[1] + 4.375) < 1e-9);   // Spitze = Abfluss
        Assert.Equal("#D2EAEC", trichter.Stil.Fuellung);
    }

    [Fact]
    public void Sinnbild_Dachablauf_BogenInGegenuhrzeigerrichtung()
    {
        // HKAP arc [0, −3, 3, 0, 180]: von 3 Uhr im Uhrzeigersinn über 6 Uhr nach 9 Uhr = untere Halbkreis;
        // KICAD (Gegenuhrzeigersinn, y oben): von = −(0 + 180) = 180 bis 360
        var p = Leer();
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, Anlegen(p, "Dachablauf", "DAB-1"), 0, 0, false, null, "mittel");
        var bogen = Assert.Single(prims, x => x.Art == "bogen");
        Assert.Equal(180, bogen.Von!.Value, 6);
        Assert.Equal(360, bogen.Bis!.Value, 6);
        Assert.Equal(2.1, bogen.Radius!.Value, 6);
    }

    [Fact]
    public void Zuordnung_JedeKlasseImKatalogUndImBibliothek()
    {
        foreach (var (klasse, key) in SchemaSymbole.Zuordnung)
        {
            Assert.True(Kat.Klassen.ContainsKey(klasse), $"{klasse} fehlt im Katalog");
            Assert.True(SchemaSymbole.Bibliothek.ContainsKey(key), $"{key} fehlt in der Bibliothek");
        }
        Assert.Equal(SchemaSymbole.Zuordnung.Count, SchemaSymbole.Zuordnung.Select(z => z.Klasse).Distinct().Count());
    }

    [Theory]
    [InlineData("Wassererwaermer", "geraet", "WE")]
    // Heizkoerper hatte hier das Kästchen «HK»; seit Modul 5 A-S 2 zeichnet er das Heizkörpersymbol der H+K-Legende
    // (SchemaSymboleHeizungTests.Heizkoerper_ZeigtDasSymbolStattDesKaestchensHK)
    public void OhneSinnbild_BleibtDasKaestchenMitKuerzel(string klasse, string schluessel, string kuerzel)
    {
        var p = Leer();
        var e = Anlegen(p, klasse, "X-1");
        Assert.Null(SchemaSymbole.SinnbildVon(p.Katalog, e));
        Assert.Null(SchemaSymbole.SinnbildAnschluss(p.Katalog, e, 0, 0));
        Assert.Equal(schluessel, SchemaSymbole.Schluessel(p, e));
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, e, 0, 0, false, null, "mittel");
        Assert.Contains(prims, x => x.Art == "polygon");
        Assert.Contains(prims, x => x.Art == "text" && x.Text == kuerzel);
    }

    [Fact]
    public void Familie_MitSchemasymbol_GehtDemSinnbildVor()
    {
        var p = Leer();
        var def = FamilieTestHilfe.Mini();
        def["kategorie"] = "Dachablauf";
        def["schemasymbol"] = JsonNode.Parse("""[ { "art": "kreis", "punkte": [[0, 0]], "radius": 1.5 } ]""");
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = def }));
        Ok(p.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "A", ["punkt"] = new JsonArray(1000, 1000, 0), ["kennzeichen"] = "DAB-F" }));
        var e = Hole(p, "DAB-F");
        Assert.NotNull(SchemaSymbole.SinnbildVon(p.Katalog, e));           // die Klasse hätte ein Sinnbild …
        Assert.Equal("familie", SchemaSymbole.Schluessel(p, e));           // … die Familie geht vor
        var (prims, _, _) = SchemaSymbole.Zeichnen(p, e, 10, 20, false, null, "mittel");
        var kreis = Assert.Single(prims);
        Assert.Equal("kreis", kreis.Art);
        Assert.Equal(1.5, kreis.Radius);
        Assert.DoesNotContain(prims, x => x.Art == "bogen");
    }

    // ------------------------------------------------------------------ im Plan (Testhaus MFH, Schema Abwasser)

    [Fact]
    public void Plan_Abwasser_ApparateAlsSinnbildMitKennzeichen()
    {
        var p = Mfh();
        var g = Grafik.Plan(p, Abwasser);
        foreach (var kz in new[] { "WC-E1", "WT-E1", "SP-E1", "DU-E1", "WM-U", "DAB-1", "KS-1" })
        {
            var t = g.Teile.Single(x => x.Id == Hole(p, kz).Id);
            Assert.Contains(t.Primitive, x => x.Art == "text" && x.Text == kz);                                   // Kennzeichen bleibt
            Assert.DoesNotContain(t.Primitive, x => x.Art == "text" && x.Text is "WC" or "WT" or "SP" or "DU" or "WM" or "DAB" or "KS");   // kein Kürzel
            Assert.Contains(t.Primitive, x => x.Art is "polygon" or "kreis" or "linie");
        }
        // der Knoten (Fangpunkt) liegt in der Mitte des Sinnbilds, der Abfluss darunter
        var s = Schemaplan.Rechnen(p, Abwasser);
        var wc = s.Graph.Knoten.Single(k => k.Kennzeichen == "WC-E1");
        Assert.Equal((wc.X, wc.Y - 4.375), SchemaSymbole.SinnbildAnschluss(p.Katalog, wc.Element, wc.X, wc.Y)!.Value);
    }

    [Fact]
    public void Plan_Sinnbilder_BleibenInnerhalbDerZeile()
    {
        // Sinnbild und Kennzeichen dürfen das Deckenband darüber nicht berühren (Zeile 50 mm, Band 2.4 mm); Abläufe sitzen seit B8d im Band
        // (Kennzeichen über dem Band, eigener Test in SchemaUntergrundTests)
        var p = Mfh();
        var plan = Schemaplan.Rechnen(p, Abwasser);
        var g = SchemaGrafik.Plan(p, plan);
        foreach (var k in plan.Graph.Knoten.Where(k => k.Art == "endgeraet" && k.Zeile is not null && k.Ablaufband is null))
        {
            var t = g.Teile.Single(x => x.Id == k.Id);
            var oben = Grafik.Begrenzung([t])![3] + 1.2;   // Kennzeichen zählt nur mit der Grundlinie, Schrifthöhe 0.75 · 1.8 = 1.35 mm
            Assert.True(oben <= k.Zeile!.Y0 + SchemaRaster.Zeilenhoehe - SchemaRaster.Deckenband / 2 + 1e-6, $"{k.Kennzeichen}: Oberkante {oben}");
        }
    }

    // ------------------------------------------------------------------ Grössentexte freistellen

    static GroessenText Text(double ax, double ay, double bx, double by, string text) =>
        new(new Grafikteil(), ax, ay, bx, by, text, 1.6, null);

    static bool Ueberdeckt(IReadOnlyList<GroessenText> l) =>
        l.Select((a, i) => l.Skip(i + 1).Any(b => GroessenTexte.Schneidet(a.Rahmen, b.Rahmen))).Any(x => x);

    [Fact]
    public void Freistellen_ZweiLinienAufEinerSpur_TextblockeOhneUeberdeckung()
    {
        // Spur y = 0: erste Linie x 0 … 24 (Mitte 12), zweite x 8 … 20 (Mitte 14); «NW 40» ist 4.8 mm breit → ungeprüft überdeckt
        var a = Text(0, 0, 24, 0, "NW 40");
        var b = Text(8, 0, 20, 0, "NW 32");
        Assert.True(Math.Abs(12 - 14) < 4.8);
        GroessenTexte.Setzen([a, b]);
        Assert.False(Ueberdeckt([a, b]));
        // der erste bleibt, der zweite ist entlang der Linie bzw. auf die andere Seite verschoben
        Assert.Equal((12.0, 0.8), (a.X, a.Y));
        Assert.True(b.Seite || Math.Abs(b.X - 14) > 0.1);
        // die Textprimitive stehen in den Grafikteilen
        var t = Assert.Single(a.Ziel.Primitive);
        Assert.Equal("NW 40", t.Text);
        Assert.Null(t.Winkel);
    }

    [Fact]
    public void Freistellen_ZweiLinienAufEinerSpur_VerschiebungKleinstmoeglich()
    {
        // gleiche Mitte: der zweite Text wandert auf die andere Seite (Versatz 0, Ausgangsseite belegt) statt entlang der Linie
        var a = Text(0, 0, 30, 0, "NW 40");
        var b = Text(0, 0, 30, 0, "NW 32");
        GroessenTexte.Setzen([a, b]);
        Assert.False(Ueberdeckt([a, b]));
        Assert.True(b.Seite);
        Assert.Equal(15.0, b.X);
        Assert.Equal(-0.8 - 0.75 * 1.6, b.Y, 6);                    // unter der Linie: Grundlinie = Unterkante des Rahmens
    }

    [Fact]
    public void Freistellen_SenkrechteSpur_RechtsDerLinieGedrehtLinks()
    {
        var a = Text(5, 0, 5, 24, "NW 100");
        var b = Text(5, 10, 5, 20, "NW 50");
        GroessenTexte.Setzen([a, b]);
        Assert.False(Ueberdeckt([a, b]));
        Assert.Equal((4.2, 12.0), (a.X, a.Y));                      // Grundlinie 0.8 links der Linie (Schrift wächst nach links), Mitte 12
        var pa = Assert.Single(a.Ziel.Primitive);
        Assert.Equal(90, pa.Winkel);
    }

    [Fact]
    public void Freistellen_Deterministisch_UndUnabhaengigVomZweitenLauf()
    {
        List<GroessenText> Lauf()
        {
            var l = new List<GroessenText> { Text(0, 0, 30, 0, "NW 40"), Text(0, 0, 30, 0, "NW 32"), Text(4, 0, 26, 0, "NW 25"), Text(0, 0, 30, 0, "NW 20") };
            GroessenTexte.Setzen(l);
            return l;
        }
        var e1 = Lauf(); var e2 = Lauf();
        Assert.Equal(e1.Select(x => (x.X, x.Y, x.Seite)), e2.Select(x => (x.X, x.Y, x.Seite)));
        Assert.False(Ueberdeckt(e1));
    }

    [Fact]
    public void Freistellen_KeinPlatz_TextBleibtAnDerAusgangslage()
    {
        // Abschnitt kürzer als der Text plus Rand: keine Verschiebung entlang, beide Seiten besetzt → bleibt, ist als überdeckt vermerkt
        var l = new List<GroessenText> { Text(0, 0, 6, 0, "NW 40"), Text(0, 0, 6, 0, "NW 32"), Text(0, 0, 6, 0, "NW 25") };
        GroessenTexte.Setzen(l);
        Assert.False(l[0].Ueberdeckt);
        Assert.False(l[1].Ueberdeckt);          // andere Seite
        Assert.True(l[2].Ueberdeckt);
        Assert.Equal(l[0].X, l[2].X);
    }

    static (double X0, double Y0, double X1, double Y1) Rahmen(Primitiv t)
    {
        double w = SchemaRaster.TextBreite(t.Text!, t.Groesse!.Value), h = 0.75 * t.Groesse!.Value;
        return t.Winkel == 90
            ? (t.Punkte[0][0] - h, t.Punkte[0][1] - w / 2, t.Punkte[0][0], t.Punkte[0][1] + w / 2)
            : (t.Punkte[0][0] - w / 2, t.Punkte[0][1], t.Punkte[0][0] + w / 2, t.Punkte[0][1] + h);
    }

    [Theory]
    [InlineData(Wasser)]
    [InlineData(Abwasser)]
    public void Plan_Groessentexte_UeberdeckenSichNicht(string ansicht)
    {
        var p = Mfh();
        // B9b (Entscheid Christian 19.10.2026): Abwasser trägt den Aussendurchmesser («110», Testhaus-da FIKTIV), Wasser NW
        var g = Grafik.Plan(p, ansicht);
        var texte = g.Teile.SelectMany(t => t.Primitive).Where(x => x.Art == "text" && x.Text is { } s && Regex.IsMatch(s, ansicht == Abwasser ? @"^(56|75|110|125)$" : @"^NW \d")).Select(Rahmen).ToList();
        aus.WriteLine($"{ansicht}: {texte.Count} Grössentexte");
        Assert.NotEmpty(texte);
        for (int i = 0; i < texte.Count; i++)
            for (int j = i + 1; j < texte.Count; j++)
            {
                var a = texte[i]; var b = texte[j];
                Assert.False(a.X0 < b.X1 && b.X0 < a.X1 && a.Y0 < b.Y1 && b.Y0 < a.Y1, $"Überdeckung: {a} und {b}");
            }
    }
}
