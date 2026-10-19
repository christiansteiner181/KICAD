using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Ordner für die erzeugten Testdateien, wird nach den Tests gelöscht.</summary>
public sealed class LageDateien : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_b4_" + Guid.NewGuid().ToString("N"));
    public string Neu(string schema, string name, LageSpec s, string unter = "")
        => IfcLageTestdatei.Schreiben(schema, Path.Combine(wurzel, unter.Length > 0 ? unter : "d", name.EndsWith(".ifc") ? name : name + ".ifc"), s);
    public void Dispose() { try { Directory.Delete(wurzel, true); } catch (IOException) { } }
}

/// <summary>Ausrichtung (Koordinatenbezug) von IFC-Dateien, transparent: Projektbezug der ersten Datei, Prüfung weiterer Dateien,
/// Abweichung mit Zahlen, Wahl projekt / gebaeude / koordinationskoerper, Reihenfolge der Grundlagen, frühere Wahl, Lagebericht.
/// Testdateien: IfcLageTestdatei (xBIM). Alle Längen in mm (Dateien in mm), Berichte in m. Erwartungswerte von Hand gerechnet.
///
/// Konventionen (IfcAusrichtung / IfcDatei): Modell = DrehungZ(−drehung) · (Welt − [x, y, z]); Abweichung = Lage dieser Datei,
/// gerechnet mit dem Projektbezug, minus gespeicherte Lage des Projekts (in Projektkoordinaten, mm, Grad).</summary>
public class IfcAusrichtungTests : IClassFixture<LageDateien>
{
    readonly LageDateien dateien;
    public IfcAusrichtungTests(LageDateien d) => dateien = d;

    // ------------------------------------------------------------------ Hilfen

    static double Z(JsonNode? n)
    {
        var v = n?.AsValue() ?? throw new InvalidOperationException("Wert fehlt");
        if (v.TryGetValue<double>(out var d)) return d;
        if (v.TryGetValue<long>(out var l)) return l;
        return v.TryGetValue<int>(out var i) ? i : throw new InvalidOperationException("keine Zahl: " + n!.ToJsonString());
    }

    static JsonObject Bezug(Projekt p) => (JsonObject)p.Modell.DerKlasse("Projektinformation").Single().Eigenschaft("ifcBezug")!.Inhalt!;

    static JsonObject UnterlageLage(Projekt p, string dateiname) =>
        (JsonObject)p.Modell.DerKlasse("Unterlage").Single(u => u.Text("original")!.EndsWith(dateiname)).Eigenschaft("lage")!.Inhalt!;

    static JsonObject Pruefen(Projekt p, string datei) => (JsonObject)Tuer.Abfrage(p, "ifc_pruefen", new JsonObject { ["datei"] = datei });

    static void BezugIst(JsonNode? bezug, double x, double y, double z, double drehung, string was = "")
    {
        Assert.True(Math.Abs(Z(bezug!["x"]) - x) <= 0.1, $"{was} x: erwartet {x}, erhalten {Z(bezug["x"])}");
        Assert.True(Math.Abs(Z(bezug["y"]) - y) <= 0.1, $"{was} y: erwartet {y}, erhalten {Z(bezug["y"])}");
        Assert.True(Math.Abs(Z(bezug["z"]) - z) <= 0.1, $"{was} z: erwartet {z}, erhalten {Z(bezug["z"])}");
        Assert.True(Math.Abs(Z(bezug["drehung"]) - drehung) <= 1e-5, $"{was} drehung: erwartet {drehung}, erhalten {Z(bezug["drehung"])}");
    }

    static void AbweichungIst(JsonNode? lage, string grundlage, double dx, double dy, double dz, double drehung)
    {
        var a = lage!["abweichung"];
        Assert.NotNull(a);
        Assert.Equal(grundlage, a!["grundlage"]!.GetValue<string>());
        Assert.True(Math.Abs(Z(a["dx"]) - dx) <= 0.2, $"dx: erwartet {dx}, erhalten {Z(a["dx"])}");
        Assert.True(Math.Abs(Z(a["dy"]) - dy) <= 0.2, $"dy: erwartet {dy}, erhalten {Z(a["dy"])}");
        Assert.True(Math.Abs(Z(a["dz"]) - dz) <= 0.2, $"dz: erwartet {dz}, erhalten {Z(a["dz"])}");
        Assert.True(Math.Abs(Z(a["drehung"]) - drehung) <= 1e-3, $"drehung: erwartet {drehung}, erhalten {Z(a["drehung"])}");
    }

    static string Vz(double meter) => meter.ToString("+0.000;-0.000;0.000");   // wie im Text der Abweichung (aktuelle Kultur)

    /// <summary>Ausgangsdatei A: Gebäudeursprung (10000, 20000, 500) mm, ohne Georeferenz, ohne Koordinationskörper.</summary>
    static LageSpec Schlicht(Pl? gebaeude = null, string marke = "") => new() { Gebaeude = gebaeude ?? new(10000, 20000, 500), Marke = marke };

    /// <summary>Datei A voll: Gebäude (10000, 20000, 500), IfcMapConversion mit Weltursprung auf Ost 2'600'000 m, Nord 1'200'000 m,
    /// Höhe 500 m (Drehung 0), Koordinationskörper bei Welt (11000, 22000, 500).</summary>
    static LageSpec Voll(string marke = "") => new()
    {
        Gebaeude = new(10000, 20000, 500), Karte = new(2600000, 1200000, 500), Koerper = new(11000, 22000, 500),
        Breite = (47, 22, 12), Laenge = (8, 32, 24), Marke = marke,
    };

    Projekt ProjektMit(string schema, LageSpec erste, string name = "arch")
    {
        var p = Leer();
        Ok(Uebernehmen(p, dateien.Neu(schema, $"{name}_{schema}", erste, "erste")));
        return p;
    }

    // ------------------------------------------------------------------ erste Datei

    [Theory]
    [InlineData("IFC4", false)]
    [InlineData("IFC2X3", false)]
    [InlineData("IFC2X3", true)]
    public void ErsteDatei_SetztDenProjektbezug_MitLandeskoordinatenUndKoordinationskoerper(string schema, bool karteAmGelaende)
    {
        var spec = Voll() with { KarteAmGelaende = karteAmGelaende };
        var datei = dateien.Neu(schema, $"erste_{schema}_{karteAmGelaende}", spec);
        var p = Leer();
        var e = Ok(Uebernehmen(p, datei));
        Assert.NotNull(e.PaketId);

        // Gebäudeursprung (10000, 20000), ±0.00 = Gebäudeursprung z (500): Median(Weltlage Geschoss − Elevation) = 500 − 0 = 3500 − 3000
        var b = Bezug(p);
        BezugIst(b, 10000, 20000, 500, 0, "Bezug");
        Assert.Contains($"erste_{schema}_{karteAmGelaende}.ifc ({schema}", b["quelle"]!.GetValue<string>());
        Assert.Contains("Lagetest", b["quelle"]!.GetValue<string>());   // Programm aus dem Kopf der Datei

        // Landeskoordinaten des Projektursprungs: Weltursprung (2'600'000, 1'200'000, 500) + Projektursprung (10, 20, 0.5) m, Drehung 0
        var landes = b["landes"]!;
        Assert.Equal(2600010.0, Z(landes["ost"]), 3);
        Assert.Equal(1200020.0, Z(landes["nord"]), 3);
        Assert.Equal(500.5, Z(landes["hoehe"]), 3);
        Assert.Equal(0.0, Z(landes["nordwinkel"]), 6);
        Assert.Equal("EPSG:2056", landes["bezugssystem"]!.GetValue<string>());
        Assert.Equal(schema == "IFC4" ? "IfcMapConversion" : "ePSet_MapConversion", landes["quelle"]!.GetValue<string>());

        // Koordinationskörper bei Welt (11000, 22000, 500) = Projekt (1000, 2000, 0), Drehung 0
        var kk = b["koordinationskoerper"]!;
        BezugIst(kk, 1000, 2000, 0, 0, "Koordinationskörper");
        Assert.Equal("Koordinationskörper", kk["name"]!.GetValue<string>());

        // Lagebericht der Unterlage: erste Datei
        var lage = UnterlageLage(p, $"erste_{schema}_{karteAmGelaende}.ifc");
        Assert.Equal("erste Datei", lage["ausrichtung"]!.GetValue<string>());
        Assert.Equal("erster", lage["bewertung"]!.GetValue<string>());
        BezugIst(lage["bezug"], 10000, 20000, 500, 0, "Lage.bezug");
        Assert.Contains(e.Hinweise, h => h.Contains("Erste IFC-Datei") && h.Contains("[10, 20, 0.5] m"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void ErsteDatei_OhneGeoreferenzUndKoerper_HatNurDenGebaeudeursprung(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var b = Bezug(p);
        BezugIst(b, 10000, 20000, 500, 0);
        Assert.Null(b["landes"]);
        Assert.Null(b["koordinationskoerper"]);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void ErsteDatei_GedrehtesGebaeude_BezugHatDieDrehung(string schema)
    {
        // Gebäude bei (1000, 2000, 0), 30° gedreht: Bezug = Gebäudeursprung + Drehung
        var p = ProjektMit(schema, Schlicht(new Pl(1000, 2000, 0, 30)));
        BezugIst(Bezug(p), 1000, 2000, 0, 30);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void DieselbeDateiErneut_ErgibtKeineAenderung_UndPassMitNullAbweichung(string schema)
    {
        var datei = dateien.Neu(schema, $"dieselbe_{schema}", Voll());
        var p = Leer();
        Ok(Uebernehmen(p, datei));
        var vorher = p.Modell.Elemente.Count;
        var zweites = Ok(Uebernehmen(p, datei));
        Assert.Null(zweites.PaketId);
        Assert.Equal(vorher, p.Modell.Elemente.Count);
        // ifc_pruefen derselben Datei: Abweichung 0 auf der genauesten Grundlage (Koordinationskörper)
        var r = Pruefen(p, datei);
        Assert.Equal("passt", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "koordinationskoerper", 0, 0, 0, 0);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void ZweiteDatei_MitAndererPruefsumme_GleicheLage_WirdOhneWahlAngenommen(string schema)
    {
        var p = ProjektMit(schema, Schlicht(marke: "a"));
        var zweite = dateien.Neu(schema, $"fach_{schema}", Schlicht(marke: "b"));
        var r = Pruefen(p, zweite);
        Assert.Equal("passt", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "gebaeude", 0, 0, 0, 0);
        var e = Ok(Uebernehmen(p, zweite));
        Assert.Contains(e.Hinweise, h => h.Contains("Lage passt zum Projektbezug"));
        var lage = UnterlageLage(p, $"fach_{schema}.ifc");
        Assert.Equal("projekt", lage["ausrichtung"]!.GetValue<string>());
        Assert.Equal("passt", lage["bewertung"]!.GetValue<string>());
        BezugIst(lage["bezug"], 10000, 20000, 500, 0);   // Projektbezug unverändert
        BezugIst(Bezug(p), 10000, 20000, 500, 0);
    }

    // ------------------------------------------------------------------ Inhaltsvergleich (Befund Abnahme 1b)

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void GleicherUrsprung_InhaltDaneben_WirdAbgelehnt_MitVersatz(string schema)
    {
        // Bernstrasse: Revit-IFC mit demselben Gebäudeursprung wie das Archicad-IFC, Inhalt aber 40 m Ost / 20 m Nord daneben
        var p = ProjektMit(schema, Schlicht(marke: "a"));
        var daneben = dateien.Neu(schema, $"daneben_{schema}", Schlicht(marke: "b") with { Wand = new(40000, 20000, 0) });
        var r = Pruefen(p, daneben);
        Assert.Equal("abweichung", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "gebaeude", 0, 0, 0, 0);   // Ursprung passt …
        Assert.Equal(0, r["lage"]!["inhalt"]!["ueberdeckung"]!.GetValue<double>());   // … der Inhalt nicht
        var nein = Uebernehmen(p, daneben);
        Assert.False(nein.Angenommen);
        Assert.Contains("ACHTUNG Inhalt", nein.Fehler);
        Assert.Contains("0 % der Bauteile im bestehenden Modell", nein.Fehler);
        Assert.Contains("Koordinationskörper", nein.Fehler);   // keine der beiden Lagen trifft
        // ausdrücklich «projekt»: wird übernommen, mit Warnung
        var ja = Ok(Uebernehmen(p, daneben, new JsonObject { ["ausrichtung"] = "projekt" }));
        Assert.Contains(ja.Hinweise, h => h.Contains("ACHTUNG: Der Inhalt liegt neben dem bestehenden Modell"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void GleicherUrsprung_InhaltImModell_Passt_MitAnteil(string schema)
    {
        var p = ProjektMit(schema, Schlicht(marke: "a"));
        var e = Ok(Uebernehmen(p, dateien.Neu(schema, $"innen_{schema}", Schlicht(marke: "b") with { Wand = new(1000, 0, 0) })));
        Assert.Contains(e.Hinweise, h => h.Contains("Lage passt zum Projektbezug") && h.Contains("100 % der Bauteile im bestehenden Modell"));
    }

    // ------------------------------------------------------------------ Toleranz 10 mm / 0.01°

    [Theory]
    [InlineData(8, 0, 0, 0.0, true)]      // |d| = 8 mm
    [InlineData(5, 5, 5, 0.0, true)]      // |d| = √75 = 8.66 mm
    [InlineData(6, 6, 0, 0.0, true)]      // |d| = √72 = 8.49 mm
    [InlineData(12, 0, 0, 0.0, false)]    // 12 mm
    [InlineData(0, 0, 11, 0.0, false)]    // 11 mm in der Höhe
    [InlineData(6, 6, 6, 0.0, false)]     // |d| = √108 = 10.39 mm > 10 (räumliche Länge, nicht je Achse)
    [InlineData(0, 0, 0, 0.005, true)]    // 0.005° ≤ 0.01°
    [InlineData(0, 0, 0, 0.02, false)]    // 0.02° > 0.01°
    [InlineData(0, 0, 0, -0.02, false)]
    public void Toleranz_10mm_und_001Grad(double dx, double dy, double dz, double drehung, bool passt)
    {
        var p = ProjektMit("IFC4", Schlicht());
        var datei = dateien.Neu("IFC4", $"tol_{dx}_{dy}_{dz}_{drehung}".Replace(',', '.'), Schlicht(new Pl(10000 + dx, 20000 + dy, 500 + dz, drehung)));
        var r = Pruefen(p, datei);
        Assert.Equal(passt ? "passt" : "abweichung", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "gebaeude", dx, dy, dz, drehung);
        var e = Uebernehmen(p, datei);
        Assert.Equal(passt, e.Angenommen);
    }

    // ------------------------------------------------------------------ Abweichung: abgelehnt, mit Zahlen

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void VerschobenesGebaeude_WirdAbgelehnt_MitZahlenUndVorschlagAusrichtung(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var elemente = p.Modell.Elemente.Count;
        // Gebäude 1250 mm Ost, 500 mm Süd, 30 mm höher: Differenz (11250−10000, 19500−20000, 530−500) = (1250, −500, 30)
        var datei = dateien.Neu(schema, $"versch_{schema}", Schlicht(new Pl(11250, 19500, 530)));

        var r = Pruefen(p, datei);
        Assert.Equal("abweichung", r["lage"]!["bewertung"]!.GetValue<string>());
        Assert.Equal("offen", r["lage"]!["ausrichtung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "gebaeude", 1250, -500, 30, 0);
        Assert.Contains("ACHTUNG Lage", r["ausrichtung"]!.GetValue<string>());
        BezugIst(r["lage"]!["bezug"], 10000, 20000, 500, 0, "gerechnet mit dem Projektbezug");

        var nein = Uebernehmen(p, datei);
        Assert.False(nein.Angenommen);
        Assert.Contains("ACHTUNG Lage", nein.Fehler);
        Assert.Contains($"{Vz(1.25)} m Ost", nein.Fehler);
        Assert.Contains($"{Vz(-0.5)} m Nord", nein.Fehler);
        Assert.Contains($"{Vz(0.03)} m Höhe", nein.Fehler);
        Assert.Contains("Gebäudeursprung", nein.Fehler);   // Grundlage genannt
        Assert.Contains("ausrichtung", nein.Vorschlag);
        Assert.Contains("projekt", nein.Vorschlag);
        Assert.Contains("gebaeude", nein.Vorschlag);
        Assert.Contains("koordinationskoerper", nein.Vorschlag);
        Assert.Equal(elemente, p.Modell.Elemente.Count);   // nichts übernommen
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Trockenlauf_ZeigtDieWarnung_RechnetMitDemProjektbezug_AendertNichts(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var elemente = p.Modell.Elemente.Count;
        var datei = dateien.Neu(schema, $"trocken_{schema}", Schlicht(new Pl(11250, 19500, 530)));
        var t = Ok(Uebernehmen(p, datei, trocken: true));
        Assert.Contains(t.Hinweise, h => h.Contains("ACHTUNG Lage") && h.Contains("Trockenlauf mit dem Projektbezug gerechnet"));
        Assert.Null(t.PaketId);
        Assert.Equal(elemente, p.Modell.Elemente.Count);
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void GedrehtesGebaeude_WirdAbgelehnt_MitDrehungInGrad(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var datei = dateien.Neu(schema, $"gedreht_{schema}", Schlicht(new Pl(10000, 20000, 500, 30)));
        var r = Pruefen(p, datei);
        Assert.Equal("abweichung", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "gebaeude", 0, 0, 0, 30);
        var nein = Uebernehmen(p, datei);
        Assert.False(nein.Angenommen);
        Assert.Contains($"{Vz(30)}°", nein.Fehler);
    }

    // ------------------------------------------------------------------ Wahl: Elemente an die richtige Stelle

    /// <summary>Datei E: Gebäude bei Welt (50000, 60000, 800), um 90° gedreht; Wand am Ursprung des Erdgeschosses (lokal (0,0), x-Richtung).</summary>
    static LageSpec DateiE(string marke = "") => Schlicht(new Pl(50000, 60000, 800, 90), marke);

    static (double[] Ursprung, double Winkel, double[] Ende) WandLage(string datei, JsonNode? bezug)
    {
        using var d = IfcDatei.Oeffnen(datei);
        d.Bezug = IfcBezug.Aus(bezug)!;
        var wand = d.Modell.Instances.OfType<Xbim.Ifc4.Interfaces.IIfcWall>().Single();
        var m = d.Lage(wand);
        return (m.Ursprung, m.WinkelZ, m.Punkt([5000, 0, 0]));
    }

    static IReadOnlyList<double> Koten(string datei, JsonNode? bezug)
    {
        using var d = IfcDatei.Oeffnen(datei);
        d.Bezug = IfcBezug.Aus(bezug)!;
        return d.Geschosse().Select(g => g.KoteMm).ToList();
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void WahlGebaeude_LegtGebaeudeUrsprungAufDenProjektursprung(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var datei = dateien.Neu(schema, $"wahl_geb_{schema}", DateiE());
        Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "gebaeude" }));

        var lage = UnterlageLage(p, $"wahl_geb_{schema}.ifc");
        Assert.Equal("gebaeude", lage["ausrichtung"]!.GetValue<string>());
        Assert.Equal("gewaehlt", lage["bewertung"]!.GetValue<string>());
        // Abweichung vorher (Gebäudeursprung gegen Projektbezug): (50000−10000, 60000−20000, 800−500), Drehung 90 − 0
        AbweichungIst(lage, "gebaeude", 40000, 40000, 300, 90);
        // Bezug dieser Datei = ihr Gebäudeursprung samt Drehung
        BezugIst(lage["bezug"], 50000, 60000, 800, 90);
        BezugIst(Bezug(p), 10000, 20000, 500, 0, "Projektbezug bleibt");

        // Wand: lokal (0,0) im EG → Projekt (0,0,0), Richtung +x (Drehung 90° der Datei wird aufgehoben), Ende (5000, 0, 0)
        var (u, w, ende) = WandLage(datei, lage["bezug"]);
        Assert.Equal([0.0, 0.0, 0.0], u.Select(x => Math.Round(x, 6)).ToArray());
        Assert.Equal(0, w, 6);
        Assert.Equal([5000.0, 0.0, 0.0], ende.Select(x => Math.Round(x, 6)).ToArray());
        // Geschosse: Kote 0 und 3000 (Elevation + (Gebäude-z 800 − Bezug-z 800)) → die zwei Geschosse des Projekts, keine neuen
        Assert.Equal([0.0, 3000.0], Koten(datei, lage["bezug"]));
        Assert.Equal(2, p.Modell.DerKlasse("Geschoss").Count());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void WahlProjekt_NimmtDieWeltkoordinatenDerDateiWieSieSind(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var datei = dateien.Neu(schema, $"wahl_proj_{schema}", DateiE());
        Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "projekt" }));

        var lage = UnterlageLage(p, $"wahl_proj_{schema}.ifc");
        Assert.Equal("projekt", lage["ausrichtung"]!.GetValue<string>());
        BezugIst(lage["bezug"], 10000, 20000, 500, 0);
        // Wand: Welt (50000, 60000, 800), 90° → Projekt (50000−10000, 60000−20000, 800−500) = (40000, 40000, 300), Drehung 90°;
        // Ende lokal (5000, 0) → Welt (50000, 65000, 800) (um 90° gedreht) → Projekt (40000, 45000, 300)
        var (u, w, ende) = WandLage(datei, lage["bezug"]);
        Assert.Equal([40000.0, 40000.0, 300.0], u.Select(x => Math.Round(x, 6)).ToArray());
        Assert.Equal(90, w, 6);
        Assert.Equal([40000.0, 45000.0, 300.0], ende.Select(x => Math.Round(x, 6)).ToArray());
        // Geschosse liegen 300 mm höher als die des Projekts (±50 mm Toleranz) → zwei neue: Kote 300 und 3300
        Assert.Equal([300.0, 3300.0], Koten(datei, lage["bezug"]));
        Assert.Equal(new[] { 0.0, 300.0, 3000.0, 3300.0 }, p.Modell.DerKlasse("Geschoss").Select(g => g.Zahl("kote")!.Value).Order().ToArray());
    }

    [Fact]
    public void AusdruecklicherBezug_GehtAllemVor_ErsteDatei_UndGeschosskoteFolgtDemBezug()
    {
        // Erste Datei mit ausdrücklichem Bezug (1000, 2000, 300, 0): ±0.00 liegt 300 mm über Welt-z 0; Gebäude-z der Datei 500
        // → Kote EG = 0 + (500 − 300) = 200, 1.OG = 3200
        var p = Leer();
        var datei = dateien.Neu("IFC4", "ausdruecklich", Schlicht());
        Ok(Uebernehmen(p, datei, new JsonObject { ["bezug"] = new JsonObject { ["x"] = 1000, ["y"] = 2000, ["z"] = 300, ["drehung"] = 0 } }));
        BezugIst(Bezug(p), 1000, 2000, 300, 0);
        Assert.Equal("ausdruecklich", UnterlageLage(p, "ausdruecklich.ifc")["ausrichtung"]!.GetValue<string>());
        Assert.Equal(new[] { 200.0, 3200.0 }, p.Modell.DerKlasse("Geschoss").Select(g => g.Zahl("kote")!.Value).Order().ToArray());

        // Weitere Datei mit abweichender Lage, aber ausdrücklichem Bezug: wird nicht abgelehnt
        var zweite = dateien.Neu("IFC4", "ausdruecklich2", Schlicht(new Pl(0, 0, 0)));
        var e = Ok(Uebernehmen(p, zweite, new JsonObject { ["bezug"] = new JsonObject { ["x"] = 5, ["y"] = 6, ["z"] = 7, ["drehung"] = 0 } }));
        Assert.Contains(e.Hinweise, h => h.Contains("Ausdrücklich vorgegebener Bezug"));
    }

    [Fact]
    public void UnbekannteAusrichtung_WirdMitListeAbgelehnt()
    {
        var p = ProjektMit("IFC4", Schlicht());
        var nein = Uebernehmen(p, dateien.Neu("IFC4", "unbekannt", Schlicht(marke: "x")), new JsonObject { ["ausrichtung"] = "mitte" });
        Assert.False(nein.Angenommen);
        Assert.Contains("mitte", nein.Fehler);
        Assert.Contains("koordinationskoerper", nein.Vorschlag);
    }

    // ------------------------------------------------------------------ Koordinationskörper

    /// <summary>Datei K: Gebäude (10000, 20000, 500), Koordinationskörper bei Welt (11000, 22000, 500) → Projekt (1000, 2000, 0).</summary>
    static LageSpec ProjektK(string marke = "") => new() { Gebaeude = new(10000, 20000, 500), Koerper = new(11000, 22000, 500), Marke = marke };

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Koordinationskoerper_PasstTrotzAnderemGebaeudeursprung(string schema)
    {
        var p = ProjektMit(schema, ProjektK());
        // Gebäude steht woanders (1000, 1000, 0), der Körper aber an derselben Weltstelle → Grundlage Koordinationskörper, Abweichung 0
        var datei = dateien.Neu(schema, $"kk_passt_{schema}", ProjektK("b") with { Gebaeude = new(1000, 1000, 0) });
        var r = Pruefen(p, datei);
        Assert.Equal("passt", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "koordinationskoerper", 0, 0, 0, 0);
        Ok(Uebernehmen(p, datei));
        Assert.Equal("projekt", UnterlageLage(p, $"kk_passt_{schema}.ifc")["ausrichtung"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Koordinationskoerper_Verschoben_GehtVorDemGebaeudeursprung_UndWahlRichtetAus(string schema)
    {
        var p = ProjektMit(schema, ProjektK());
        // Gebäudeursprung identisch (!), Körper bei Welt (12250, 21700, 510): im Projekt (2250, 1700, 10) statt (1000, 2000, 0)
        // → Abweichung (1250, −300, 10) auf der Grundlage Koordinationskörper
        var datei = dateien.Neu(schema, $"kk_versch_{schema}", ProjektK("b") with { Koerper = new(12250, 21700, 510) });
        var r = Pruefen(p, datei);
        Assert.Equal("abweichung", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "koordinationskoerper", 1250, -300, 10, 0);
        Assert.False(Uebernehmen(p, datei).Angenommen);

        Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "koordinationskoerper" }));
        var lage = UnterlageLage(p, $"kk_versch_{schema}.ifc");
        Assert.Equal("koordinationskoerper", lage["ausrichtung"]!.GetValue<string>());
        // Bezug = Welt-Lage des Körpers − Projektlage (1000, 2000, 0) = (11250, 19700, 510), Drehung 0
        BezugIst(lage["bezug"], 11250, 19700, 510, 0);
        // Körper liegt jetzt auf (1000, 2000, 0); die Wand (Welt (10000, 20000, 500)) auf (−1250, 300, −10)
        var (u, w, _) = WandLage(datei, lage["bezug"]);
        Assert.Equal([-1250.0, 300.0, -10.0], u.Select(x => Math.Round(x, 6)).ToArray());
        Assert.Equal(0, w, 6);
        // Geschosskoten −10 / 2990 liegen innerhalb ±50 mm der vorhandenen → keine neuen Geschosse
        Assert.Equal(2, p.Modell.DerKlasse("Geschoss").Count());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Koordinationskoerper_Gedreht_BezugHatDieDrehungDerDatei(string schema)
    {
        var p = ProjektMit(schema, ProjektK());
        // Datei B um 90° gedreht: Gebäude Welt (3000, 4000, 0, 90°), Körper Welt (5000, 6000, 0, 90°)
        var spec = new LageSpec { Gebaeude = new(3000, 4000, 0, 90), Koerper = new(5000, 6000, 0, 90), Marke = "b" };
        var datei = dateien.Neu(schema, $"kk_rot_{schema}", spec);
        var r = Pruefen(p, datei);
        // Körper im Projekt (mit Projektbezug): (5000−10000, 6000−20000, 0−500) = (−5000, −14000, −500); gespeichert (1000, 2000, 0)
        // → (−6000, −16000, −500); Drehung 90° − 0° − 0° = 90°
        AbweichungIst(r["lage"], "koordinationskoerper", -6000, -16000, -500, 90);

        Ok(Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "koordinationskoerper" }));
        var lage = UnterlageLage(p, $"kk_rot_{schema}.ifc");
        // Bezug: Drehung 90°; Körper-Projektlage (1000, 2000) um 90° gedreht = (−2000, 1000); Ursprung = Welt (5000, 6000) − (−2000, 1000) = (7000, 5000)
        BezugIst(lage["bezug"], 7000, 5000, 0, 90);
        // Probe: Körper → R(−90°)·((5000, 6000) − (7000, 5000)) = R(−90°)·(−2000, 1000) = (1000, 2000) ✓.
        // Wand lokal (0,0) im EG: Welt (3000, 4000), 90° → Projekt R(−90°)·(−4000, −1000) = (−1000, 4000), Drehung 0; Ende (4000, 4000)
        var (u, w, ende) = WandLage(datei, lage["bezug"]);
        Assert.Equal([-1000.0, 4000.0, 0.0], u.Select(x => Math.Round(x, 6)).ToArray());
        Assert.Equal(0, w, 6);
        Assert.Equal([4000.0, 4000.0, 0.0], ende.Select(x => Math.Round(x, 6)).ToArray());
    }

    [Fact]
    public void WahlKoordinationskoerper_OhneKoerperInDerDatei_WirdMitHinweisAbgelehnt()
    {
        var p = ProjektMit("IFC4", ProjektK());
        var datei = dateien.Neu("IFC4", "kk_fehlt", Schlicht(new Pl(11250, 19500, 530)));
        var nein = Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "koordinationskoerper" });
        Assert.False(nein.Angenommen);
        Assert.Contains("die Datei hat keinen", nein.Fehler);
        Assert.Contains("Koordinationskörper", nein.Vorschlag);
    }

    [Fact]
    public void WahlKoordinationskoerper_OhneKoerperImProjekt_WirdMitHinweisAbgelehnt()
    {
        var p = ProjektMit("IFC4", Schlicht());
        var datei = dateien.Neu("IFC4", "kk_projekt_fehlt", ProjektK("b") with { Gebaeude = new(11250, 19500, 530) });
        var nein = Uebernehmen(p, datei, new JsonObject { ["ausrichtung"] = "koordinationskoerper" });
        Assert.False(nein.Angenommen);
        Assert.Contains("das Projekt hat noch keinen", nein.Fehler);
    }

    [Theory]
    [InlineData("Koordinationskörper")]
    [InlineData("KOORDINATIONSKÖRPER 1")]
    [InlineData("Koordinationskoerper")]
    [InlineData("Nullpunktkörper")]
    [InlineData("Coordination Body")]
    [InlineData("Koordinationswürfel")]
    public void Koordinationskoerper_WirdAmNamenErkannt(string name)
    {
        var datei = dateien.Neu("IFC4", "kk_name_" + name.Replace(' ', '_'), new LageSpec { Koerper = new(1000, 2000, 3000), KoerperName = name });
        using var d = IfcDatei.Oeffnen(datei);
        var lage = d.Lage();
        Assert.NotNull(lage.KoordinationskoerperWelt);
        Assert.Equal([1000.0, 2000.0, 3000.0], lage.KoordinationskoerperWelt!);
        Assert.Equal(name, lage.KoordinationskoerperName);
    }

    [Fact]
    public void Koordinationskoerper_AndererNameBraucht_DenParameter()
    {
        var datei = dateien.Neu("IFC4", "kk_anders", new LageSpec { Koerper = new(1000, 2000, 3000), KoerperName = "Passpunkt Alpha" });
        using var d = IfcDatei.Oeffnen(datei);
        Assert.Null(d.Lage().KoordinationskoerperWelt);
        var l = d.Lage("passpunkt");
        Assert.Equal([1000.0, 2000.0, 3000.0], l.KoordinationskoerperWelt!);
        Assert.Equal("Passpunkt Alpha", l.KoordinationskoerperName);
        Assert.Null(d.Lage("gibtesnicht").KoordinationskoerperWelt);
    }

    [Fact]   // B4-2 behoben am 17.10.2026 (Hauptlauf)
    public void ErsteDatei_AndererKoerperName_WirdImProjektbezugGespeichert()
    {
        var p = Leer();
        var datei = dateien.Neu("IFC4", "kk_param_erste", new LageSpec { Gebaeude = new(10000, 20000, 500), Koerper = new(11000, 22000, 500), KoerperName = "Passpunkt Alpha" });
        Ok(Uebernehmen(p, datei, new JsonObject { ["koordinationskoerper"] = "Passpunkt" }));
        var kk = Bezug(p)["koordinationskoerper"];
        Assert.NotNull(kk);
        BezugIst(kk, 1000, 2000, 0, 0);
    }

    // ------------------------------------------------------------------ Reihenfolge der Grundlagen

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Grundlage_Reihenfolge_Koerper_vor_Landeskoordinaten_vor_Gebaeudeursprung(string schema)
    {
        var p = ProjektMit(schema, Voll());
        // Alle drei weichen verschieden ab: Gebäude 300 mm Ost, Weltursprung der Karte 2 m Ost, Körper 500 mm Ost
        var alles = Voll("b") with { Gebaeude = new(10300, 20000, 500), Karte = new(2600002, 1200000, 500), Koerper = new(11500, 22000, 500) };

        var r1 = Pruefen(p, dateien.Neu(schema, $"reihe1_{schema}", alles));
        // Körper: im Projekt (11500−10000, 22000−20000, 0) = (1500, 2000, 0) gegen (1000, 2000, 0) → dx = 500
        AbweichungIst(r1["lage"], "koordinationskoerper", 500, 0, 0, 0);

        // Ohne Körper: Landeskoordinaten. Projektursprung als Weltpunkt (10000, 20000, 500) in dieser Datei = (2600002 + 10, 1200000 + 20, 500 + 0.5)
        // gegen gespeichert (2600010, 1200020, 500.5) → Ost +2 m = 2000 mm, Nord 0, Höhe 0
        var r2 = Pruefen(p, dateien.Neu(schema, $"reihe2_{schema}", alles with { Koerper = null }));
        AbweichungIst(r2["lage"], "landeskoordinaten", 2000, 0, 0, 0);

        // Weder Körper noch Karte: Gebäudeursprung (10300 − 10000 = 300)
        var r3 = Pruefen(p, dateien.Neu(schema, $"reihe3_{schema}", alles with { Koerper = null, Karte = null }));
        AbweichungIst(r3["lage"], "gebaeude", 300, 0, 0, 0);
        Assert.Contains("nur Hinweis", r3["lage"]!["abweichung"]!["text"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Landeskoordinaten_PassenTrotzAnderemGebaeudeursprung_UndHoeheZaehlt(string schema)
    {
        var p = ProjektMit(schema, Voll());
        // Gebäude im Ursprung, aber dieselbe Karte → derselbe Landespunkt → passt (Gebäudeursprung allein würde 10/20/0.5 m abweichen)
        var passt = dateien.Neu(schema, $"land_passt_{schema}", new LageSpec { Karte = new(2600000, 1200000, 500), Marke = "b" });
        var r = Pruefen(p, passt);
        Assert.Equal("passt", r["lage"]!["bewertung"]!.GetValue<string>());
        AbweichungIst(r["lage"], "landeskoordinaten", 0, 0, 0, 0);
        // Höhe der Karte 1 m höher: Projektursprung 1 m höher → dz = +1000
        var hoch = dateien.Neu(schema, $"land_hoch_{schema}", new LageSpec { Karte = new(2600000, 1200000, 501), Marke = "c" });
        AbweichungIst(Pruefen(p, hoch)["lage"], "landeskoordinaten", 0, 0, 1000, 0);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Landeskoordinaten_GedrehteKarte_AbweichungInProjektachsen(string schema)
    {
        var p = ProjektMit(schema, Voll());
        // Karte um 90° gedreht (x-Achse der Datei zeigt nach Nord): Weltpunkt (10000, 20000, 500) mm → Landes
        // E = 2600000 + 10·cos90 − 20·sin90 = 2600000 − 20; N = 1200000 + 10·sin90 + 20·cos90 = 1200000 + 10; gespeichert (2600010, 1200020)
        // → Landes-Differenz (−30 m Ost, −10 m Nord); in Achsen der Datei (um 90° gedreht: x = Nord, y = West): dx = −10 m, dy = +30 m
        var datei = dateien.Neu(schema, $"land_rot_{schema}", new LageSpec { Karte = new(2600000, 1200000, 500, 90), Marke = "b" });
        var r = Pruefen(p, datei);
        AbweichungIst(r["lage"], "landeskoordinaten", -10000, 30000, 0, 90);
        Assert.Equal("abweichung", r["lage"]!["bewertung"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ frühere Wahl

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void FruehereWahl_GiltWieder_SolangeDieAbweichungGleichBleibt_SonstNeueAblehnung(string schema)
    {
        var p = ProjektMit(schema, Schlicht());
        var v1 = dateien.Neu(schema, "fassung.ifc", DateiE("v1"), "v1");
        Ok(Uebernehmen(p, v1, new JsonObject { ["ausrichtung"] = "gebaeude" }));

        // Fassung 2: gleiche Datei neu exportiert (andere Prüfsumme), gleiche Lage → ohne Parameter angenommen, Wahl «gebaeude» gilt
        var v2 = dateien.Neu(schema, "fassung.ifc", DateiE("v2"), "v2");
        var r2 = Pruefen(p, v2);
        Assert.Equal("gebaeude", r2["lage"]!["ausrichtung"]!.GetValue<string>());
        Assert.Equal("gewaehlt", r2["lage"]!["bewertung"]!.GetValue<string>());
        Ok(Uebernehmen(p, v2));
        Assert.Equal("gebaeude", UnterlageLage(p, "fassung.ifc")["ausrichtung"]!.GetValue<string>());

        // Fassung 3: 5 mm verschoben – noch innerhalb 10 mm Toleranz der Abweichung → Wahl gilt weiter
        var v3 = dateien.Neu(schema, "fassung.ifc", Schlicht(new Pl(50005, 60000, 800, 90), "v3"), "v3");
        Assert.Equal("gebaeude", Pruefen(p, v3)["lage"]!["ausrichtung"]!.GetValue<string>());
        Ok(Uebernehmen(p, v3));

        // Fassung 4: Gebäude 500 mm weiter weg → Abweichung hat sich geändert: erneut ablehnen mit Hinweis, Zahlen neu
        var v4 = dateien.Neu(schema, "fassung.ifc", Schlicht(new Pl(50500, 60000, 800, 90), "v4"), "v4");
        var r4 = Pruefen(p, v4);
        Assert.Equal("abweichung", r4["lage"]!["bewertung"]!.GetValue<string>());
        Assert.Equal("offen", r4["lage"]!["ausrichtung"]!.GetValue<string>());
        Assert.Contains("hat sich seit der letzten Fassung geändert", r4["lage"]!["geaendert"]!.GetValue<string>());
        AbweichungIst(r4["lage"], "gebaeude", 40500, 40000, 300, 90);
        var nein = Uebernehmen(p, v4);
        Assert.False(nein.Angenommen);
        Assert.Contains("hat sich seit der letzten Fassung geändert", nein.Fehler);
        // … mit ausdrücklicher Wahl geht es wieder
        Ok(Uebernehmen(p, v4, new JsonObject { ["ausrichtung"] = "gebaeude" }));
        AbweichungIst(UnterlageLage(p, "fassung.ifc"), "gebaeude", 40500, 40000, 300, 90);
    }

    [Fact]
    public void FruehereWahl_GiltNurFuerDieselbeUnterlage_EineAndereDateiWirdNeuGefragt()
    {
        var p = ProjektMit("IFC4", Schlicht());
        Ok(Uebernehmen(p, dateien.Neu("IFC4", "eine.ifc", DateiE("a"), "u1"), new JsonObject { ["ausrichtung"] = "gebaeude" }));
        var andere = Uebernehmen(p, dateien.Neu("IFC4", "andere.ifc", DateiE("b"), "u2"));
        Assert.False(andere.Angenommen);
        Assert.Contains("ACHTUNG Lage", andere.Fehler);
    }

    // ------------------------------------------------------------------ Lagebericht

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Lagebericht_GelaendeAufLandeskoordinaten_MitKoerper_OhneKarte(string schema)
    {
        // Gelände auf Landeskoordinaten (Welt = LV95-ähnlich): Site bei (2'600'000 m, 1'200'000 m, 500 m); Gebäude relativ (12 m, 8 m, 0);
        // Körper relativ zum Gelände (3 m, 1 m, 0)
        var spec = new LageSpec
        {
            Gelaende = new(2600000000, 1200000000, 500000), Gebaeude = new(12000, 8000, 0), Koerper = new(3000, 1000, 0),
            Breite = (47, 22, 12), Laenge = (8, 32, 24),
        };
        var p = Leer();
        var r = Pruefen(p, dateien.Neu(schema, $"lage_land_{schema}", spec));
        var lage = r["lage"]!;
        var gel = lage["gelaende"]!["lage"]!.AsArray();
        Assert.Equal([2600000.0, 1200000.0, 500.0], gel.Select(Z).ToArray());
        Assert.Equal([2600012.0, 1200008.0, 500.0], lage["gebaeude"]!["lage"]!.AsArray().Select(Z).ToArray());
        Assert.Equal(0, Z(lage["gelaende"]!["drehung"]), 6);
        Assert.Equal(500.0, Z(lage["nullHoehe"]), 4);
        Assert.Equal("Koordinationskörper", lage["koordinationskoerper"]!["name"]!.GetValue<string>());
        Assert.Equal([2600003.0, 1200001.0, 500.0], lage["koordinationskoerper"]!["lage"]!.AsArray().Select(Z).ToArray());
        BezugIst(lage["eigenerBezug"], 2600012000, 1200008000, 500000, 0);
        Assert.Null(lage["landes"]);   // keine IfcMapConversion
        // Breite 47° 22' 12" = 47 + 22/60 + 12/3600 = 47.37°, Länge 8° 32' 24" = 8 + 32/60 + 24/3600 = 8.54°
        Assert.Equal(47.37, Z(lage["geografisch"]!["breite"]), 6);
        Assert.Equal(8.54, Z(lage["geografisch"]!["laenge"]), 6);
        Assert.False(string.IsNullOrWhiteSpace(lage["hinweis"]!.GetValue<string>()));
        Assert.Equal("erster", lage["bewertung"]!.GetValue<string>());
        Assert.Equal(Path.GetFileName($"lage_land_{schema}.ifc"), lage["datei"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Lagebericht_MitKarteUndGedrehtemGebaeude_RechnetDenLandespunktDesGebaeudeUrsprungs(string schema)
    {
        // Karte: Weltursprung bei (2'600'000, 1'200'000, 500) m, x-Achse 10° gegen Ost; Gebäude bei Welt (10 m, 20 m, 0.5 m), 30° gedreht
        // Landespunkt des Gebäudeursprungs: E = E0 + 10·cos10° − 20·sin10° = E0 + 9.848078 − 3.472964 = E0 + 6.375114
        //                                   N = N0 + 10·sin10° + 20·cos10° = N0 + 1.736482 + 19.696155 = N0 + 21.432637
        //                                   H = 500 + 0.5; Nordwinkel = Kartendrehung 10° + Gebäudedrehung 30° = 40°
        var spec = new LageSpec { Gebaeude = new(10000, 20000, 500, 30), Karte = new(2600000, 1200000, 500, 10) };
        var p = Leer();
        var lage = Pruefen(p, dateien.Neu(schema, $"lage_karte_{schema}", spec))["lage"]!;
        var l = lage["landes"]!;
        Assert.Equal(schema == "IFC4" ? "IfcMapConversion" : "ePSet_MapConversion", l["quelle"]!.GetValue<string>());
        Assert.Equal("EPSG:2056", l["bezugssystem"]!.GetValue<string>());
        Assert.Equal(1.0, Z(l["massstab"]), 9);
        Assert.Equal(2600006.375, Z(l["gebaeudeNullpunkt"]!["ost"]), 3);
        Assert.Equal(1200021.433, Z(l["gebaeudeNullpunkt"]!["nord"]), 3);
        Assert.Equal(500.5, Z(l["gebaeudeNullpunkt"]!["hoehe"]), 3);
        Assert.Equal(40.0, Z(l["nordwinkel"]), 5);
        Assert.Null(lage["koordinationskoerper"]);
        Assert.Equal(30.0, Z(lage["gebaeude"]!["drehung"]), 6);
        Assert.Equal(0.5, Z(lage["nullHoehe"]), 4);
    }

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Lagebericht_OhneGeorefUndKoerper_HatKeineLandesUndKoerperAngaben(string schema)
    {
        var lage = Pruefen(Leer(), dateien.Neu(schema, $"lage_nix_{schema}", Schlicht()))["lage"]!;
        Assert.Null(lage["landes"]);
        Assert.Null(lage["koordinationskoerper"]);
        Assert.Null(lage["geografisch"]);
        Assert.Equal([0.0, 0.0, 0.0], lage["gelaende"]!["lage"]!.AsArray().Select(Z).ToArray());
        Assert.Equal([10.0, 20.0, 0.5], lage["gebaeude"]!["lage"]!.AsArray().Select(Z).ToArray());
        Assert.Equal(0.5, Z(lage["nullHoehe"]), 4);
    }

    [Fact]
    public void Pruefen_ZeigtDenProjektbezugUndAendertNichts()
    {
        var p = ProjektMit("IFC4", Voll());
        var elemente = p.Modell.Elemente.Count;
        var r = Pruefen(p, dateien.Neu("IFC4", "pruefen_bezug", Voll("b")));
        Assert.NotNull(r["bezugProjekt"]);
        BezugIst(r["bezugProjekt"], 10000, 20000, 500, 0);
        Assert.Equal(elemente, p.Modell.Elemente.Count);
    }

    // ------------------------------------------------------------------ Mehrere Dateien, gemischte Fassungen

    [Fact]
    public void GemischteFassungen_Ifc4Architektur_Ifc2x3Fachplaner_GleicheLage_PasstOhneWahl()
    {
        var p = ProjektMit("IFC4", Voll());
        // IFC2x3-Datei mit ePSet-Georeferenz, anderer Gebäudeursprung (Revit: Gebäude im Ursprung), gleiche Landeskoordinaten
        var fach = dateien.Neu("IFC2X3", "fach_gemischt", new LageSpec { Karte = new(2600000, 1200000, 500), Marke = "f" });
        Ok(Uebernehmen(p, fach));
        Assert.Equal("passt", UnterlageLage(p, "fach_gemischt.ifc")["bewertung"]!.GetValue<string>());
        Assert.Equal(2, p.Modell.DerKlasse("Unterlage").Count());
    }
}
