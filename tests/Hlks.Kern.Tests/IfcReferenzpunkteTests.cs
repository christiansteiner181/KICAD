namespace Hlks.Kern.Tests;

/// <summary>Referenzpunkte (Kegel) – die Bewertung ohne Datei, von Hand nachgerechnet (Gesamtprüfung 13.11.2026). Fiktive Zahlen in der
/// Grössenordnung von Landeskoordinaten (mm). Projektbezug Bp = (2 600 800 m, 1 200 300 m), Drehung 0; Kegel des Projekts P1 = (2 600 830, 1 200 380),
/// P2 = (2 601 050, 1 200 610), P3 = (2 600 830, 1 200 610) m. Die Datei hat eigene Koordinaten, um 90° gedreht: D = R(−90°)·(P − P1), also
/// D1 = (0, 0), D2 = (230, −220) m, D3 = (230, 0) m. Gesucht B, d mit R(−d)·(D − B) = P − Bp: d = −90°, B = D1 − R(−90°)·(P1 − Bp) =
/// (0, 0) − R(−90°)·(30, 80) = (−80, 30) m. Probe D2: D2 − B = (310, −250), R(+90°) → (250, 310) = P2 − Bp.</summary>
public sealed class IfcReferenzpunkteTests
{
    static readonly IfcBezug Proj = new(2_600_800_000, 1_200_300_000, 0, 0);
    static readonly double[] P1 = [2_600_830_000, 1_200_380_000, 0], P2 = [2_601_050_000, 1_200_610_000, 0], P3 = [2_600_830_000, 1_200_610_000, 0];
    static readonly double[] D1 = [0, 0, 0], D2 = [230_000, -220_000, 0], D3 = [230_000, 0, 0];
    static readonly IfcBezug Eigener = new(5000, 5000, 0, 0);

    [Fact]
    public void ZweiKegel_InEigenenKoordinaten_SindNichtEindeutig_HalbeDrehung()
    {
        // Kegel 1 auf P1 und 2 auf P2 oder umgekehrt: beide Lagen treffen beide Kegel – es darf keine stillschweigend gelten
        var r = IfcReferenzpunkte.Bewerten([D1, D2], [], Proj, Eigener, [P1, P2])!;
        Assert.Null(r.Bezug);
        Assert.Equal("unsicher", r.Bericht["ergebnis"]!.GetValue<string>());
        Assert.Equal(2, r.Bericht["trefferJeLage"]!["referenzkegelLagen"]!.GetValue<int>());
        Assert.Contains("halbe Drehung", r.Text);
    }

    [Fact]
    public void DreiKegel_InEigenenKoordinaten_ErgebenDieVonHandGerechneteLage()
    {
        var r = IfcReferenzpunkte.Bewerten([D1, D2, D3], [], Proj, Eigener, [P1, P2, P3])!;
        Assert.Equal("referenzkegel", r.Lage);
        Assert.Equal(-80_000, r.Bezug!.X, 1);
        Assert.Equal(30_000, r.Bezug.Y, 1);
        Assert.Equal(-90, r.Bezug.Drehung, 6);
        Assert.Equal(3, r.Bericht["trefferJeLage"]!["referenzkegel"]!.GetValue<int>());
    }

    [Fact]
    public void KegelSchonImProjektbezug_LageProjektGilt_AuchInAndererReihenfolge()
    {
        // Die Datei liegt in den Koordinaten des Projekts, ihre Kegel stehen nur in anderer Reihenfolge in der Datei
        var r = IfcReferenzpunkte.Bewerten([P2, P1], [], Proj, Eigener, [P1, P2])!;
        Assert.Equal("projekt", r.Lage);
        Assert.Equal(Proj, r.Bezug);
    }
}
