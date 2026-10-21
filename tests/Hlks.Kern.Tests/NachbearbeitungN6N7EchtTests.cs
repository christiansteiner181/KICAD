using Xbim.Ifc4.Interfaces;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N6/N7 mit dem echten Archicad-IFC von Bernstrasse 42 («440_Bernstrasse 42 3D Modell.ifc», IFC2X3,
/// Archicad 27.0.0; lokal unter %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\440_Bernstrasse_42_3D_Modell.ifc, sonst die
/// inhaltsgleiche architekt_3d.ifc – gleiche MD5 67b1cc70…, geprüft 21.10.2026). Nie im Repository. Fehlt die Datei, überspringt
/// sich der Test (wie IfcEchtRauchTests). Erwartete Zahlen aus dem IFC nachgerechnet (Platzierungskette, Extrusion, Körper):
///   N6: 23 IfcDoor mit OverallHeight 5.19 m, Körper 2.15 m hoch, Öffnung z −3.04 … 2.15 m (z. B. #39395 «A81»).
///   N7: UZ-14 = IfcBeam #28246 14Vr3s2CbAHvvM3Iyi1MNt (1.OG), UZ-62 = #32964 0vatlF1TnEHBPVJoESIoDl (2.OG): H-Profil 220 × 220
///       über 9.435 m von (14661.0, −54130.1) nach (7725.1, −47733.8), Oberkante 5924 bzw. 8984 mm.</summary>
public class NachbearbeitungN6N7EchtTests(ITestOutputHelper aus)
{
    sealed class EchtDateiFehlt(string grund) : Exception("$XunitDynamicSkip$" + grund);

    static readonly object sperre = new();
    static (Projekt P, BefehlsErgebnis E, string Pfad)? lauf;

    static string Datei() =>
        Echt(@"bernstrasse42\440_Bernstrasse_42_3D_Modell.ifc") ?? Echt(@"bernstrasse42\architekt_3d.ifc")
        ?? throw new EchtDateiFehlt(@"Echte Datei fehlt auf diesem Rechner: %USERPROFILE%\HLKS_CAD_testdaten\bernstrasse42\440_Bernstrasse_42_3D_Modell.ifc");

    static (Projekt P, BefehlsErgebnis E, string Pfad) Lauf()
    {
        var pfad = Datei();
        lock (sperre)
        {
            if (lauf is { } da) return da;
            var p = Leer();
            var e = Ok(Uebernehmen(p, pfad));
            lauf = (p, e, pfad);
            return lauf.Value;
        }
    }

    static Element MitGlobalId(Projekt p, string klasse, string gid) =>
        p.Modell.DerKlasse(klasse).SingleOrDefault(e => e.Kennungen?.GetValueOrDefault("ifc") == gid) ?? throw new Xunit.Sdk.XunitException($"{klasse} {gid} fehlt");

    [Fact]
    public void N6_TuerenAusArchicad_HoeheAusDemKoerper_JedeImHinweis()
    {
        var (p, e, _) = Lauf();
        var a81 = MitGlobalId(p, "Tuer", "3m9C58HOr7avoPMgbZ52Dj");   // IfcDoor #39395 «A81»
        Assert.Equal(2150, a81.Zahl("hoehe"));
        Assert.Equal(940, a81.Zahl("breite"));
        Assert.Contains("Körper IfcDoor #39395", a81.Eigenschaft("hoehe")!.Herkunft.Quelle);
        Assert.Contains("OverallHeight 5190 mm", a81.Eigenschaft("hoehe")!.Herkunft.Quelle);

        var tueren = p.Modell.DerKlasse("Tuer").ToList();
        var ausKoerper = tueren.Where(t => t.Eigenschaft("hoehe")!.Herkunft.Quelle!.Contains("OverallHeight 5190 mm")).ToList();
        foreach (var t in ausKoerper) Assert.Equal(2150, t.Zahl("hoehe"));
        // Keine Tür ist mehr höher als die höchste Tür im IFC (Lift, Körper 2470 mm; OverallHeight höchstens 2685 mm)
        Assert.All(tueren, t => Assert.True(t.Zahl("hoehe") <= 2700, $"{t.Kennzeichen} «{t.Name}» {t.Zahl("hoehe")} mm"));
        // Die übrigen behalten OverallHeight (Rahmen bis 170 mm über der Öffnung bleiben unter der Schwelle)
        Assert.All(tueren.Except(ausKoerper), t => Assert.EndsWith("IfcDoor.OverallHeight", t.Eigenschaft("hoehe")!.Herkunft.Quelle));
        var hinweis = Assert.Single(e.Hinweise, h => h.StartsWith("Architektur: Tür: Höhe aus dem Körper statt OverallHeight"));
        Assert.Contains($"– {ausKoerper.Count}: ", hinweis);
        foreach (var t in ausKoerper) Assert.Contains($"«{t.Name}» 2150 statt 5190 mm", hinweis);
        Assert.DoesNotContain(e.Hinweise, h => h.StartsWith("Architektur: Fenster: Höhe aus dem Körper"));
        aus.WriteLine($"{ausKoerper.Count} von {tueren.Count} übernommenen Türen mit Höhe aus dem Körper; {hinweis.Length} Zeichen Hinweis");
        // Die 23 IfcDoor mit 5.19 m stehen alle in einer übernommenen Wand (Probelauf 21.10.2026)
        Assert.Equal(23, ausKoerper.Count);
    }

    [Fact]
    public void N7_StahlUnterzuege_AchseUndLaengeWieImIfc()
    {
        var (p, _, _) = Lauf();
        var (an, en) = NachbearbeitungN6N7Testdatei.UzErwartet();
        foreach (var (gid, unterkante) in new[] { ("14Vr3s2CbAHvvM3Iyi1MNt", 5704.0), ("0vatlF1TnEHBPVJoESIoDl", 8764.0) })
        {
            var uz = MitGlobalId(p, "Traeger", gid);
            var g = uz.Geometrie!;
            aus.WriteLine($"{uz.Kennzeichen} {gid}: [{g.Punkte[0][0]}, {g.Punkte[0][1]}, {g.Punkte[0][2]}] → [{g.Punkte[1][0]}, {g.Punkte[1][1]}]");
            for (int k = 0; k < 2; k++)
            {
                Assert.True(Math.Abs(an[k] - g.Punkte[0][k]) <= 1, $"{uz.Kennzeichen} Anfang [{g.Punkte[0][0]}, {g.Punkte[0][1]}], erwartet [{an[0]:0.0}, {an[1]:0.0}]");
                Assert.True(Math.Abs(en[k] - g.Punkte[1][k]) <= 1, $"{uz.Kennzeichen} Ende [{g.Punkte[1][0]}, {g.Punkte[1][1]}], erwartet [{en[0]:0.0}, {en[1]:0.0}]");
            }
            Assert.Equal(9435, Math.Sqrt(Math.Pow(g.Punkte[1][0] - g.Punkte[0][0], 2) + Math.Pow(g.Punkte[1][1] - g.Punkte[0][1], 2)), 0);
            Assert.Equal(unterkante, g.Punkte[0][2], 0);
            Assert.Equal(220, g.Dicke);
            Assert.Equal("stahl", uz.Text("material"));
        }
    }

    [Fact]
    public void N7_AlleTraeger_LiegenInIhremIfcKoerperUndImGebaeude()
    {
        var (p, _, pfad) = Lauf();
        using var d = IfcDatei.Oeffnen(pfad);   // erste IFC-Datei im Projekt: Projektbezug 0 – Projekt = Welt der Datei
        var geo = IfcGeometrie.Von(d);
        var beams = d.Modell.Instances.OfType<IIfcBeam>().ToDictionary(b => b.GlobalId.ToString());
        var waende = p.Modell.DerKlasse("Wand").SelectMany(w => w.Geometrie!.Punkte).ToList();
        double wx0 = waende.Min(q => q[0]) - 500, wx1 = waende.Max(q => q[0]) + 500, wy0 = waende.Min(q => q[1]) - 500, wy1 = waende.Max(q => q[1]) + 500;
        var traeger = p.Modell.DerKlasse("Traeger").ToList();
        Assert.True(traeger.Count >= 70, $"{traeger.Count} Träger");
        // Körper des Trägers, sonst seiner Teile (Zerlegung, wie der Übertrag)
        var teile = d.Modell.Instances.OfType<IIfcRelAggregates>().SelectMany(r => r.RelatedObjects.OfType<IIfcProduct>().Select(k => (r.RelatingObject.EntityLabel, k)))
            .ToLookup(x => x.EntityLabel, x => x.k);
        double[] Begrenzung(IIfcBeam b)
        {
            if (geo.Koerper(b, false) is { } n) return n.Begrenzung()!;
            var gesamt = new Dreiecksnetz();
            foreach (var k in teile[b.EntityLabel]) if (geo.Koerper(k, false) is { } tn) gesamt.Anhaengen(tn);
            return gesamt.Begrenzung() ?? throw new Xunit.Sdk.XunitException($"IfcBeam #{b.EntityLabel} ohne Körper");
        }
        foreach (var t in traeger)
        {
            var bb = Begrenzung(beams[t.Kennungen!["ifc"]]);
            foreach (var q in t.Geometrie!.Punkte)
            {
                Assert.True(q[0] >= bb[0] - 2 && q[0] <= bb[3] + 2 && q[1] >= bb[1] - 2 && q[1] <= bb[4] + 2,
                    $"{t.Kennzeichen} «{t.Name}» Punkt [{q[0]}, {q[1]}] ausserhalb seines IFC-Körpers [{bb[0]:0} … {bb[3]:0}, {bb[1]:0} … {bb[4]:0}]");
                Assert.True(q[0] >= wx0 && q[0] <= wx1 && q[1] >= wy0 && q[1] <= wy1, $"{t.Kennzeichen} «{t.Name}» Punkt [{q[0]}, {q[1]}] ausserhalb der Wände");
            }
        }
    }
}
