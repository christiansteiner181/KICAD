using System.Diagnostics;
using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Rauchtests mit den echten Projektdateien (%USERPROFILE%\HLKS_CAD_testdaten, nie im Repo): Abfrage ifc_pruefen auf
/// Bernstrasse 42 (Archicad 27 IFC2x3, Revit 2025/2024 IFC2x3) und BKW Hauptsitz (Archicad 28 IFC4, Trimble Nova IFC4).
/// Fehlt eine Datei, überspringt sich der Test. Erwartete Anzahlen stammen aus dem «Befund Probelauf» in entwurf/modul-1b.md
/// (Zählung der Entitäten im STEP-Text) bzw. sind als «beobachtet 17.10.2026» gekennzeichnet.</summary>
public class IfcEchtRauchTests
{
    const string Bern = "bernstrasse42";
    const string Bkw = "bkw_hauptsitz";
    const string Arch3d = Bern + "/architekt_3d.ifc";
    const string Hk2026 = Bern + "/hlks_hk_2026-02-10.ifc";
    const string Hk2024 = Bern + "/hlks_hk_2024-09-17.ifc";
    const string BkwArch = Bkw + "/BKW-HS_1_ARCH_ARC.ifc";
    const string BkwHk = Bkw + "/BKW-HS_1_HK-GEBT_NOV.ifc";
    const string BkwSa = Bkw + "/BKW-HS_1_SA-GEBT_NOV.ifc";
    const string BkwLu = Bkw + "/BKW-HS_1_LU-GEBT_NOV.ifc";

    static string Datei(string rel)
    {
        var f = Echt(rel.Replace('/', Path.DirectorySeparatorChar));
        if (f is null) throw new EchtDateiFehlt($"Echte Datei fehlt auf diesem Rechner: %USERPROFILE%\\HLKS_CAD_testdaten\\{rel.Replace('/', '\\')}");
        return f;
    }

    /// <summary>xUnit v2 überspringt einen Test, dessen Ausnahme-Meldung mit «$XunitDynamicSkip$» beginnt (kein Assert.Skip in 2.9.3).</summary>
    sealed class EchtDateiFehlt(string grund) : Exception("$XunitDynamicSkip$" + grund);

    sealed record Lauf(JsonObject R, double Sekunden, string Pfad)
    {
        public int Z(string klasse) => R["zuordnung"]![klasse]?.GetValue<int>() ?? 0;
        public int Ohne(string entitaet) => R["ohneKlasse"]![entitaet]?.GetValue<int>() ?? 0;
        public JsonArray Geschosse => R["geschosse"]!.AsArray();
        public double[] Koten => Geschosse.Select(g => g!["kote"]!.GetValue<double>()).ToArray();
        public string Schema => R["schema"]!.GetValue<string>();
        public string Programm => R["programm"]!.GetValue<string>();
        public JsonNode Lage => R["lage"]!;
    }

    static readonly Dictionary<string, Lauf> Zwischenspeicher = [];

    /// <summary>ifc_pruefen auf einem leeren Projekt, gemessen; das Ergebnis wird für die weiteren Tests der Datei wiederverwendet.</summary>
    static Lauf Pruefen(string rel)
    {
        var pfad = Datei(rel);
        lock (Zwischenspeicher)
        {
            if (Zwischenspeicher.TryGetValue(rel, out var da)) return da;
            var sw = Stopwatch.StartNew();
            var r = (JsonObject)Tuer.Abfrage(Leer(), "ifc_pruefen", new JsonObject { ["datei"] = pfad });
            return Zwischenspeicher[rel] = new Lauf(r, sw.Elapsed.TotalSeconds, pfad);
        }
    }

    /// <summary>Anzahl IfcBuildingStorey in der Datei und Anzahl verschiedener Koten (±10 mm), unabhängig vom Produktcode gezählt.</summary>
    static (int Roh, int Verschiedene) GeschosseRoh(string pfad)
    {
        using var d = IfcDatei.Oeffnen(pfad);
        var el = d.Modell.Instances.OfType<IIfcBuildingStorey>().Select(s => s.Elevation.HasValue ? (double)s.Elevation.Value * d.MmJeEinheit : double.NaN).ToList();
        return (el.Count, el.Where(e => !double.IsNaN(e)).Select(e => Math.Round(e / 10)).Distinct().Count());
    }

    // ------------------------------------------------------------------ für alle sieben Dateien

    public static IEnumerable<object[]> Alle() =>
    [
        [Arch3d, "IFC2X3", "Archicad 27", "m", 7, 0],
        [Hk2026, "IFC2X3", "Revit 2025", "mm", 7, 37],
        [Hk2024, "IFC2X3", "Revit 2024", "mm", 7, 37],
        [BkwArch, "IFC4", "Archicad 28", "m", 10, 0],
        [BkwHk, "IFC4", "Trimble Nova", "m", 9, 0],
        [BkwSa, "IFC4", "Trimble Nova", "m", 9, 0],
        [BkwLu, "IFC4", "Trimble Nova", "m", 9, 0],
    ];

    [Theory]
    [MemberData(nameof(Alle))]
    public void Pruefen_Fassung_Programm_Einheit_Geschosse_Raeume_Lagebericht_Dauer(string rel, string schema, string programm, string einheit, int geschosse, int raeume)
    {
        var l = Pruefen(rel);
        Assert.Equal(schema, l.Schema);
        Assert.Contains(programm, l.Programm);
        Assert.Equal(einheit, l.R["einheit"]!.GetValue<string>());
        Assert.Equal(raeume, l.R["raeume"]!.GetValue<int>());
        // Geschosse: zusammengefasst nach Kote (±10 mm). Unabhängig gezählt: verschiedene Elevation-Werte in der Datei.
        var (roh, verschieden) = GeschosseRoh(l.Pfad);
        Assert.Equal(geschosse, verschieden);
        Assert.Equal(geschosse, l.Geschosse.Count);
        Assert.Equal(roh, l.Geschosse.Sum(g => g!["ifc"]!.GetValue<int>()));   // kein IfcBuildingStorey geht verloren
        // Lagebericht: Gelände, Gebäude, ±0.00, eigener Bezug, Bewertung «erster» (leeres Projekt)
        Assert.NotNull(l.Lage["gelaende"]!["lage"]);
        Assert.NotNull(l.Lage["gebaeude"]!["lage"]);
        Assert.NotNull(l.Lage["nullHoehe"]);
        Assert.NotNull(l.Lage["eigenerBezug"]);
        Assert.Equal("erster", l.Lage["bewertung"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(l.R["ausrichtung"]!.GetValue<string>()));
        // Zeit: ifc_pruefen (inkl. Laden) unter 60 s je Datei
        Assert.True(l.Sekunden < 60, $"{rel}: {l.Sekunden:0.0} s");
        // Hinweis «keine Räume» nur, wenn wirklich keine da sind
        Assert.Equal(raeume == 0, l.R["hinweis"] is { } h && h.ToString().Contains("keine Räume"));
    }

    [Theory]
    [MemberData(nameof(Alle))]
    public void Pruefen_JedesProduktIstEntwederZugeordnetOderOhneKlasse(string rel, string schema, string programm, string einheit, int geschosse, int raeume)
    {
        var l = Pruefen(rel);
        int zugeordnet = l.R["zuordnung"]!.AsObject().Sum(k => k.Value!.GetValue<int>());
        int ohne = l.R["ohneKlasse"]!.AsObject().Sum(k => k.Value!.GetValue<int>());
        using var d = IfcDatei.Oeffnen(l.Pfad);
        int produkte = d.Modell.Instances.OfType<IIfcProduct>()
            .Count(x => x is not (IIfcSpatialStructureElement or IIfcDistributionPort or IIfcOpeningElement or IIfcVirtualElement or IIfcAnnotation));
        Assert.Equal(produkte, zugeordnet + ohne);
        Assert.True(zugeordnet > 0);
    }

    // ------------------------------------------------------------------ Bernstrasse 42: Architektur (Archicad 27, IFC2x3, m)

    [Fact]
    public void Bernstrasse_Architektur_Geschosse_Koten()
    {
        // 7 Geschosse: 2 UG (−5.70, −3.06), EG 0, 1. OG 3.06, 2. OG 6.12, Attika 9.18, Dach 12.48 (Archicad-Namen UG, UG, EG, OG, OG, Attika, Dach)
        var l = Pruefen(Arch3d);
        Assert.Equal([-5.7, -3.06, 0, 3.06, 6.12, 9.18, 12.48], l.Koten);
        Assert.Equal(1360, l.Geschosse[2]!["produkte"]!.GetValue<int>());   // EG (beobachtet 17.10.2026)
    }

    [Fact]
    public void Bernstrasse_Architektur_Zuordnung_StimmtMitDerZaehlungDesProbelaufsUeberein()
    {
        var l = Pruefen(Arch3d);
        Assert.Equal(57, l.Z("Tuer"));            // 57 IfcDoor
        Assert.Equal(36, l.Z("Fenster"));         // 36 IfcWindow
        Assert.Equal(73, l.Z("Decke"));           // 73 IfcSlab (FLOOR 54, LANDING 17, BASESLAB 2)
        Assert.Equal(51, l.Z("Stuetze"));         // beobachtet 17.10.2026
        Assert.Equal(73, l.Z("Traeger"));         // beobachtet 17.10.2026
        Assert.Equal(3691, l.Ohne("IfcBuildingElementPart"));   // Schichtteile gehören zur Wand, werden nicht einzeln übernommen
    }

    [Fact]   // B4-1 behoben am 17.10.2026 (Hauptlauf)
    public void Bernstrasse_Architektur_AlleWaendeSindWand()
    {
        var l = Pruefen(Arch3d);
        Assert.Equal(402 + 9, l.Z("Wand"));
        Assert.Equal(0, l.Ohne("IfcWall"));
    }

    [Fact]   // B4-3 behoben am 17.10.2026 (Hauptlauf)
    public void Dach_IfcRoof_MitTypobjekt_IstDach()
    {
        Assert.Equal(0, Pruefen(BkwArch).Ohne("IfcRoof"));
        Assert.Equal(0, Pruefen(Hk2026).Ohne("IfcRoof"));
    }

    // ------------------------------------------------------------------ Bernstrasse 42: HLKS Revit

    [Fact]
    public void Bernstrasse_HlksRevit2026_Geschosse_ZweiGebaeudeZusammengefasst()
    {
        // Revit exportiert Architektur-Link mit: zwei Gebäude → Geschosse doppelt (ifc = 2), ausser 1. UG (nur ein Gebäude)
        var l = Pruefen(Hk2026);
        Assert.Equal([-5.7, -3.06, 0, 3.06, 6.12, 9.18, 12.48], l.Koten);
        Assert.Equal([2, 1, 2, 2, 2, 2, 2], l.Geschosse.Select(g => g!["ifc"]!.GetValue<int>()).ToArray());
        Assert.Equal(13, GeschosseRoh(l.Pfad).Roh);
    }

    [Fact]
    public void Bernstrasse_HlksRevit2026_Zuordnung_SummenPassenZuDenEntitaetenDesProbelaufs()
    {
        var l = Pruefen(Hk2026);
        // 1952 IfcFlowSegment = Rohr + Kanal + nicht zuordenbare
        Assert.Equal(1952, l.Z("Rohr") + l.Z("Kanal") + l.Ohne("IfcFlowSegment"));
        Assert.True(l.Z("Rohr") > 1000);
        // 1426 IfcFlowFitting
        Assert.Equal(1426, new[] { "Bogen", "TStueck", "Reduktion", "Reinigungsoeffnung", "Hausanschluss", "Verteiler" }.Sum(l.Z) + l.Ohne("IfcFlowFitting"));
        // 140 IfcFlowTerminal
        Assert.Equal(140, new[] { "WC", "Waschtisch", "Dusche", "Badewanne", "Spueltisch", "Ausgussbecken", "Waschmaschine", "Bodenablauf", "Dachablauf", "Zuluftdurchlass", "Abluftdurchlass" }.Sum(l.Z) + l.Ohne("IfcFlowTerminal"));
        // 64 IfcFlowController (Armaturen) und Zähler; seit Modul 2 sind die VAV-Boxen (IfcAirTerminalBoxType) Volumenstromregler
        Assert.Equal(64, l.Z("Ventil") + l.Z("Wasserzaehler") + l.Z("Waermezaehler") + l.Z("Volumenstromregler") + l.Ohne("IfcFlowController"));
        Assert.True(l.Z("Volumenstromregler") > 0);
        Assert.Equal(37, l.R["raeume"]!.GetValue<int>());
    }

    [Fact]
    public void Bernstrasse_HlksRevit2024_GleicheGeschosseWie2026_EinGebaeude()
    {
        var alt = Pruefen(Hk2024); var neu = Pruefen(Hk2026);
        Assert.Equal(neu.Koten, alt.Koten);
        Assert.All(alt.Geschosse, g => Assert.Equal(1, g!["ifc"]!.GetValue<int>()));
        Assert.True(alt.Z("Rohr") > 500);
    }

    // ------------------------------------------------------------------ BKW Hauptsitz: Architektur (Archicad 28, IFC4, m)

    [Fact]
    public void Bkw_Architektur_Geschosse_ZehnMitAttikaDachUndDG2()
    {
        // 3 UG (−11.80, −7.60, −4.20), EG, 3 OG (3, 6, 9), Attika 12.10, Dachaufsicht 15.49 und «HS2 DG2» 18.543 (2 Produkte) = 10
        var l = Pruefen(BkwArch);
        Assert.Equal([-11.8, -7.6, -4.2, 0, 3, 6, 9, 12.1, 15.49, 18.543], l.Koten);
        Assert.Equal("Attika", l.Geschosse[7]!["name"]!.GetValue<string>());
        Assert.Equal("Dachaufsicht", l.Geschosse[8]!["name"]!.GetValue<string>());
    }

    [Fact]
    public void Bkw_Architektur_Zuordnung_StimmtMitDerZaehlungDesProbelaufsUeberein()
    {
        var l = Pruefen(BkwArch);
        Assert.Equal(2411, l.Z("Wand"));                         // 2411 IfcWall
        Assert.Equal(865, l.Z("Decke") + l.Z("Dach"));           // 804 IfcSlab (ROOF → Dach) + 61 IfcRoof (seit B4-3 Dach)
        Assert.Equal(299, l.Z("Tuer"));                          // 299 IfcDoor
        Assert.Equal(621, l.Z("Fenster") + l.Ohne("IfcWindow")); // 621 IfcWindow (3 SKYLIGHT ohne Klasse)
        Assert.Equal(691, l.Z("Stuetze"));                       // beobachtet 17.10.2026
        Assert.Equal(396, l.Z("Traeger"));                       // beobachtet 17.10.2026
        Assert.Equal(0, l.R["raeume"]!.GetValue<int>());
    }

    [Fact]   // B4-7 behoben am 17.10.2026 (Hauptlauf)
    public void Bkw_Architektur_AlleFensterSindFenster()
    {
        Assert.Equal(621, Pruefen(BkwArch).Z("Fenster"));
    }

    // ------------------------------------------------------------------ BKW Hauptsitz: Fachplaner (Trimble Nova, IFC4, m)

    [Fact]
    public void Bkw_Heizung_Zuordnung_RohrUeber1000_UndSummenDesProbelaufs()
    {
        var l = Pruefen(BkwHk);
        Assert.Equal(1521, l.Z("Rohr"));                          // 1521 IfcPipeSegment
        Assert.True(l.Z("Rohr") > 1000);
        // 1910 IfcPipeFitting (+ 13 IfcDuctFitting, die als TStueck der Lüftung zählen)
        Assert.Equal(1910 + 13, l.Z("Bogen") + l.Z("TStueck") + l.Z("Reduktion") + l.Z("Verteiler") + l.Z("Reinigungsoeffnung") + l.Z("Hausanschluss") + l.Ohne("IfcPipeFitting") + l.Ohne("IfcDuctFitting"));
        Assert.Equal(238, l.Z("Ventil") + l.Ohne("IfcValve"));    // 238 IfcValve
        Assert.Equal(32, l.Z("Pumpe"));                           // 32 IfcPump
        Assert.Equal(13, l.Z("Wasserzaehler"));                   // 13 IfcFlowMeter
        Assert.Equal(7, l.Z("Waermeerzeuger"));                   // 7 IfcHeatExchanger
        Assert.Equal(4, l.Z("Speicher"));                         // 4 IfcTank
        Assert.Equal(0, l.R["raeume"]!.GetValue<int>());
    }

    [Fact]   // B4-5 behoben am 17.10.2026 (Hauptlauf)
    public void Bkw_Heizung_AlleVentileSindVentil()
    {
        var l = Pruefen(BkwHk);
        Assert.Equal(238, l.Z("Ventil"));
        Assert.Equal(0, l.Ohne("IfcValve"));
    }

    [Fact]
    public void Bkw_Sanitaer_Zuordnung()
    {
        var l = Pruefen(BkwSa);
        Assert.Equal(99, l.Z("Rohr"));                            // 99 IfcPipeSegment
        Assert.Equal(112, l.Z("Bogen") + l.Z("TStueck") + l.Z("Reduktion") + l.Z("Verteiler") + l.Z("Reinigungsoeffnung") + l.Z("Hausanschluss") + l.Ohne("IfcPipeFitting"));   // 112 IfcPipeFitting
        Assert.Equal(1, l.Z("Speicher"));                         // 1 IfcTank
    }

    [Fact]
    public void Bkw_Lueftung_Zuordnung()
    {
        var l = Pruefen(BkwLu);
        Assert.Equal(75, l.Z("Kanal"));                           // 75 IfcDuctSegment
        Assert.Equal(81, l.Z("Bogen") + l.Z("TStueck") + l.Z("Reduktion") + l.Ohne("IfcDuctFitting"));   // 81 IfcDuctFitting
        Assert.Equal(0, l.Z("Rohr"));
    }

    [Fact]
    public void Bkw_Fachplaner_HabenAlleDieselbenNeunGeschosse_WieDieArchitekturOhneDG2()
    {
        var arch = Pruefen(BkwArch).Koten;
        foreach (var rel in new[] { BkwHk, BkwSa, BkwLu })
            Assert.Equal(arch.Take(9), Pruefen(rel).Koten);
    }

    [Fact]   // B4-4 behoben am 17.10.2026 (Hauptlauf)
    public void Bkw_Nova_GeografischeBreiteLaenge_SindPlausibel()
    {
        var g = Pruefen(BkwHk).Lage["geografisch"];
        if (g is null) return;
        Assert.InRange(g["breite"]!.GetValue<double>(), -90, 90);
        Assert.InRange(g["laenge"]!.GetValue<double>(), -180, 180);
    }

    // ------------------------------------------------------------------ Ausrichtung mit den echten Dateien

    static IfcAusrichtungsErgebnis Gegen(string erste, string zweite, string? wahl = null)
    {
        using var d1 = IfcDatei.Oeffnen(Datei(erste));
        var projektBezug = IfcAusrichtung.ProjektBezug(d1, d1.BezugVorschlag());
        using var d2 = IfcDatei.Oeffnen(Datei(zweite));
        return IfcAusrichtung.Bestimmen(d2, projektBezug, null, wahl, null, null);
    }

    static double Z(JsonNode? n) => n!.GetValue<double>();

    [Fact]
    public void Lage_Bernstrasse_RevitGegenRevit_2024_und_2026_Passt()
    {
        // Beide Revit-Dateien: Gebäudeursprung (0, 0, 583.69 m), Drehung 0
        var a = Gegen(Hk2026, Hk2024);
        Assert.Equal("passt", a.Bewertung);
        Assert.Equal(0, Z(a.Bericht["abweichung"]!["dx"]), 1);
        Assert.Equal(0, Z(a.Bericht["abweichung"]!["dz"]), 1);
    }

    [Fact]
    public void Lage_Bernstrasse_ArchicadGegenRevit_HoehenunterschiedWirdMitZahlGemeldet()
    {
        // Archicad: Welt-z des Gebäudes 0, Höhe 583.69 m steht nur in IfcSite.RefElevation. Revit: Welt-z des Gebäudes 583.69 m.
        // Gebäudeursprung gegen Projektbezug (Archicad zuerst): Δz = 583.69 m = 583690 mm, x/y gleich (0, 0), Drehung 0
        var a = Gegen(Arch3d, Hk2026);
        Assert.Equal("abweichung", a.Bewertung);
        var ab = a.Bericht["abweichung"]!;
        Assert.Equal("gebaeude", ab["grundlage"]!.GetValue<string>());
        Assert.Equal(0, Z(ab["dx"]), 1);
        Assert.Equal(0, Z(ab["dy"]), 1);
        Assert.Equal(583690, Z(ab["dz"]), 1);
        Assert.Equal(0, Z(ab["drehung"]), 4);
        Assert.Contains("ACHTUNG Lage", a.Text);
        Assert.Contains("583.690 m Höhe", a.Text.Replace(',', '.'));
    }

    [Fact]
    public void Lage_Bernstrasse_WahlGebaeude_LegtDieRevitGeschosseAufDieKotenDerArchitektur()
    {
        var wahl = Gegen(Arch3d, Hk2026, "gebaeude");
        Assert.Equal("gewaehlt", wahl.Bewertung);
        using var d = IfcDatei.Oeffnen(Datei(Hk2026));
        d.Bezug = wahl.Bezug;
        // EG der Revit-Datei (Welt-z 583.69 m) liegt jetzt auf ±0.00 wie das EG der Architektur
        Assert.Equal(Pruefen(Arch3d).Koten.Select(k => Math.Round(k * 1000, 1)), d.Geschosse().Select(g => g.KoteMm));
        // «projekt» (Welt wie sie ist) würde die Revit-Geschosse 583.69 m über die Architektur legen
        using var d2 = IfcDatei.Oeffnen(Datei(Hk2026));   // Geschosse() merkt sich sein Ergebnis: neue Instanz für den anderen Bezug
        d2.Bezug = Gegen(Arch3d, Hk2026, "projekt").Bezug;
        Assert.Equal(583690, d2.Geschosse().Single(g => Math.Abs(g.KoteMm - 583690) < 1).KoteMm, 1);
    }

    [Fact]
    public void Lage_Bkw_NovaDateienUntereinander_PassenMitNullAbweichung()
    {
        foreach (var zweite in new[] { BkwSa, BkwLu })
        {
            var a = Gegen(BkwHk, zweite);
            Assert.Equal("passt", a.Bewertung);
            var ab = a.Bericht["abweichung"]!;
            Assert.True(Math.Abs(Z(ab["dx"])) < 1 && Math.Abs(Z(ab["dy"])) < 1 && Math.Abs(Z(ab["dz"])) < 1, $"{zweite}: {ab.ToJsonString()}");
            Assert.True(Math.Abs(Z(ab["drehung"])) < 0.001);
        }
    }

    [Fact]
    public void Lage_Bkw_NovaGegenArchicad_GebaeudeursprungHatAbstandVon177_5Metern()
    {
        // Beide Dateien liegen in Landeskoordinaten (Gelände = Gebäude = Weltlage), aber die Programme setzen den Gebäudeursprung verschieden:
        //   Archicad (2'600'899.233, 1'200'543.440, 560.65), −18.324697°;  Nova (2'600'830.000, 1'200'380.000, 560.65), −18.339990°
        //   Differenz Welt: (2'600'830.000 − 2'600'899.233, 1'200'380.000 − 1'200'543.440) = (−69.233, −163.440) m, Abstand √(4793.2 + 26712.3) = 177.50 m
        //   im Projekt (Archicad-Achsen, Drehung +18.3247°): x' = −69.233·0.949290 − (−163.440)·0.314402 = −65.72 + 51.39 = −14.34 m,
        //                                                       y' = −69.233·0.314402 + (−163.440)·0.949290 = −21.77 − 155.15 = −176.92 m
        //   Drehung: −18.339990 − (−18.324697) = −0.015293°;  Höhe 0 (beide 560.65 m)
        var a = Gegen(BkwArch, BkwHk);
        Assert.Equal("abweichung", a.Bewertung);
        var ab = a.Bericht["abweichung"]!;
        Assert.Equal("gebaeude", ab["grundlage"]!.GetValue<string>());
        Assert.Equal(177.50, Math.Sqrt(Z(ab["dx"]) * Z(ab["dx"]) + Z(ab["dy"]) * Z(ab["dy"])) / 1000, 1);
        Assert.Equal(-14.34, Z(ab["dx"]) / 1000, 1);
        Assert.Equal(-176.92, Z(ab["dy"]) / 1000, 1);
        Assert.Equal(0, Z(ab["dz"]), 1);
        Assert.Equal(-0.0153, Z(ab["drehung"]), 3);
    }

    [Fact]
    public void Lage_Bkw_WahlProjekt_NimmtDieLandeskoordinatenWieSieSind_GeschosseDeckenSichMitDerArchitektur()
    {
        // Beide in Landeskoordinaten → «projekt» ist hier die richtige Wahl: Bezug = Archicad-Bezug, die Nova-Geschosse haben dieselben Koten
        var a = Gegen(BkwArch, BkwHk, "projekt");
        Assert.Equal("gewaehlt", a.Bewertung);
        using var arch = IfcDatei.Oeffnen(Datei(BkwArch));
        var eigener = arch.BezugVorschlag();   // im Projekt auf 0.1 mm und 1e-6° gerundet gespeichert
        Assert.True(Math.Abs(eigener.X - a.Bezug.X) < 0.1 && Math.Abs(eigener.Y - a.Bezug.Y) < 0.1 && Math.Abs(eigener.Z - a.Bezug.Z) < 0.1 && Math.Abs(eigener.Drehung - a.Bezug.Drehung) < 1e-5);
        using var hk = IfcDatei.Oeffnen(Datei(BkwHk));
        hk.Bezug = a.Bezug;
        Assert.Equal(Pruefen(BkwArch).Koten.Take(9).Select(k => Math.Round(k * 1000, 1)), hk.Geschosse().Select(g => g.KoteMm));
    }
}
