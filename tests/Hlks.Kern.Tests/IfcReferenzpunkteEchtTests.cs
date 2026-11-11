using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Referenzpunkte beim Ausrichten (Christian 11.11.2026: «bei den meisten modellen gibt es referenz punkte zum ausrichten. Bsp Kegel … Siehe
/// BKWHS letzte gescheiterte einpassung», «die kegel liegen meist abseits des gebäudes») mit den echten Dateien BKW Hauptsitz aus
/// %USERPROFILE%\HLKS_CAD_testdaten (nur lokal); fehlt eine Datei, kehrt der Test früh zurück.</summary>
public sealed class IfcReferenzpunkteEchtTests
{
    [Fact]
    public void BkwHs_KegelImArchitekturmodell_GebaeudetechnikLiegtMitLageProjektAufDemKegel()
    {
        if (Echt("bkw_hauptsitz/BKW-HS_1_ARCH_ARC.ifc") is not { } arch || Echt("bkw_hauptsitz/BKW-HS_1_HK-GEBT_NOV.ifc") is not { } hk) return;
        using var a = IfcDatei.Oeffnen(arch);
        var kegel = a.Referenzkegel();
        // Archicad: Typ «Kegel 27», zwei Kegel abseits des Gebäudes auf den Landeskoordinaten (mm)
        Assert.Equal(2, kegel.Count);
        Assert.Contains(kegel, k => Math.Abs(k[0] - 2600830000) < 1 && Math.Abs(k[1] - 1200380000) < 1);
        Assert.Contains(kegel, k => Math.Abs(k[0] - 2601050000) < 1 && Math.Abs(k[1] - 1200610000) < 1);
        var proj = a.BezugVorschlag();   // Projektbezug des BKW-Projekts: erste Datei = Architektur

        // Trimble Nova (Gebäudetechnik): keine Wände, keine Kegel – der Gebäudeursprung liegt auf dem ersten Kegel, also gilt die Lage «projekt»
        using var h = IfcDatei.Oeffnen(hk);
        Assert.Empty(h.Referenzkegel());
        var r = IfcReferenzpunkte.Pruefen(h, proj, h.BezugVorschlag(), kegel);
        Assert.NotNull(r);
        Assert.Equal("projekt", r!.Lage);
        Assert.Equal(proj, r.Bezug);
        Assert.Equal("ursprung", r.Bericht["punkte"]!.GetValue<string>());
    }
}
