namespace Hlks.Kern.Tests;

/// <summary>4×4-Lagen aus IFC (Import/Matrix.cs): Achsensystem, Verkettung, Punkt/Richtung, Inverse, Drehwinkel.
/// Erwartete Werte von Hand gerechnet (Spaltenvektoren, p' = M · p, Drehung positiv gegen den Uhrzeigersinn).</summary>
public class MatrixTests
{
    const double Tol = 1e-9;

    static void Gleich(double[] erwartet, double[] ist, double tol = Tol)
    {
        Assert.Equal(erwartet.Length, ist.Length);
        for (int i = 0; i < erwartet.Length; i++) Assert.True(Math.Abs(erwartet[i] - ist[i]) <= tol, $"Komponente {i}: erwartet {erwartet[i]}, erhalten {ist[i]}");
    }

    [Fact]
    public void Einheit_LaesstPunkteUndRichtungenUnveraendert()
    {
        var e = Matrix4.Einheit;
        Gleich([1, 2, 3], e.Punkt([1, 2, 3]));
        Gleich([1, 2, 3], e.Richtung([1, 2, 3]));
        Gleich([0, 0, 0], e.Ursprung);
        Gleich([1, 0, 0], e.AchseX); Gleich([0, 1, 0], e.AchseY); Gleich([0, 0, 1], e.AchseZ);
        Assert.Equal(0, e.WinkelZ, 9);
    }

    [Fact]
    public void Default_VerhaeltSichWieEinheit()
    {
        // Ein nicht initialisierter Wert (z. B. Feld ohne Zuweisung) darf nicht abstürzen
        var d = default(Matrix4);
        Assert.Equal(1, d[0, 0]); Assert.Equal(0, d[0, 1]); Assert.Equal(1, d[3, 3]);
        Gleich([4, 5, 6], d.Punkt([4, 5, 6]));
        Gleich([4, 5, 6], (d * Matrix4.Verschiebung(4, 5, 6)).Punkt([0, 0, 0]));
    }

    [Fact]
    public void Achsen_UrsprungXUndZ_SpaltenSindDieAchsen()
    {
        // o = (1,2,3), x = (0,1,0), z = (0,0,1); y = z × x = (0·0−1·1, 1·0−0·0, 0·1−0·0) = (−1, 0, 0)
        var m = Matrix4.Achsen([1, 2, 3], [0, 1, 0], [0, 0, 1]);
        Gleich([0, 1, 0], m.AchseX);
        Gleich([-1, 0, 0], m.AchseY);
        Gleich([0, 0, 1], m.AchseZ);
        Gleich([1, 2, 3], m.Ursprung);
        Gleich([1, 3, 3], m.Punkt([1, 0, 0]));   // o + 1·x
        Gleich([0, 2, 3], m.Punkt([0, 1, 0]));   // o + 1·y
        Gleich([1, 2, 8], m.Punkt([0, 0, 5]));   // o + 5·z
        Assert.Equal(90, m.WinkelZ, 9);
    }

    [Fact]
    public void Achsen_MachtXRechtwinkligZuZ_UndNormiert()
    {
        // x = (3,0,4), z = (0,0,2): z normiert (0,0,1); x − (x·z)z = (3,0,4) − 4·(0,0,1) = (3,0,0) → (1,0,0)
        var m = Matrix4.Achsen([0, 0, 0], [3, 0, 4], [0, 0, 2]);
        Gleich([1, 0, 0], m.AchseX); Gleich([0, 1, 0], m.AchseY); Gleich([0, 0, 1], m.AchseZ);
    }

    [Fact]
    public void Achsen_OhneRichtungen_IstEineReineVerschiebung()
    {
        var m = Matrix4.Achsen([7, 8, 9], null, null);
        Gleich([8, 10, 12], m.Punkt([1, 2, 3]));
        Assert.Equal(0, m.WinkelZ, 9);
    }

    [Fact]
    public void Achsen_ZAlsOstRichtung_WaehltXAlsNord()
    {
        // z = (1,0,0) (|z.x| ≥ 0.9): x-Vorgabe (0,1,0); y = z × x = (1,0,0) × (0,1,0) = (0,0,1)
        var m = Matrix4.Achsen([0, 0, 0], null, [1, 0, 0]);
        Gleich([0, 1, 0], m.AchseX); Gleich([0, 0, 1], m.AchseY); Gleich([1, 0, 0], m.AchseZ);
    }

    [Fact]
    public void Achsen_ZLaengeNull_FaelltAufZAchse()
    {
        var m = Matrix4.Achsen([0, 0, 0], [1, 0, 0], [0, 0, 0]);
        Gleich([0, 0, 1], m.AchseZ);
    }

    [Fact]
    public void Verkettung_WirktVonRechtsNachLinks()
    {
        var v = Matrix4.Verschiebung(10, 0, 0);
        var d = Matrix4.DrehungZ(90);
        // (V·D)·(1,0,0): erst drehen (1,0,0) → (0,1,0), dann verschieben → (10,1,0)
        Gleich([10, 1, 0], (v * d).Punkt([1, 0, 0]), 1e-12);
        // (D·V)·(1,0,0): erst verschieben → (11,0,0), dann drehen → (0,11,0)
        Gleich([0, 11, 0], (d * v).Punkt([1, 0, 0]), 1e-12);
    }

    [Fact]
    public void Verkettung_IstAssoziativ_UndEinheitIstNeutral()
    {
        var a = Matrix4.Achsen([1, 2, 3], [1, 1, 0], [0, 0, 1]);
        var b = Matrix4.Verschiebung(-4, 5, 6);
        var c = Matrix4.DrehungZ(33);
        var p = new double[] { 7, -8, 9 };
        Gleich(((a * b) * c).Punkt(p), (a * (b * c)).Punkt(p), 1e-9);
        Gleich(a.Punkt(p), (Matrix4.Einheit * a).Punkt(p), 1e-12);
        Gleich(a.Punkt(p), (a * Matrix4.Einheit).Punkt(p), 1e-12);
    }

    [Fact]
    public void Punkt_ZweiKomponenten_ZGleichNull()
    {
        Gleich([1, 2, 5], Matrix4.Verschiebung(0, 0, 5).Punkt([1, 2]));
        Gleich([1, 0, 5], Matrix4.Verschiebung(0, 0, 5).Punkt([1]));
    }

    [Fact]
    public void Richtung_IgnoriertDieVerschiebung_FolgtDerDrehung()
    {
        var m = Matrix4.Verschiebung(100, 200, 300) * Matrix4.DrehungZ(90);
        Gleich([0, 1, 0], m.Richtung([1, 0, 0]), 1e-12);   // Drehung 90°: x → y, keine Verschiebung
        Gleich([-1, 0, 0], m.Richtung([0, 1, 0]), 1e-12);
        Gleich([0, 0, 1], m.Richtung([0, 0, 1]), 1e-12);
        Gleich([0, 1], m.Richtung([1, 0]).Take(2).ToArray(), 1e-12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(90)]
    [InlineData(-45)]
    [InlineData(135)]
    [InlineData(-170)]
    public void DrehungZ_PositivGegenDenUhrzeigersinn_UndWinkelZGibtSieZurueck(double grad)
    {
        var m = Matrix4.DrehungZ(grad);
        double w = grad * Math.PI / 180;
        Gleich([Math.Cos(w), Math.Sin(w), 0], m.Punkt([1, 0, 0]));
        Gleich([-Math.Sin(w), Math.Cos(w), 0], m.Punkt([0, 1, 0]));
        Assert.Equal(grad, m.WinkelZ, 9);
        Gleich([0, 0, 0], m.Ursprung);
    }

    [Fact]
    public void DrehungZ_90Grad_XWirdY_NullUndVollkreisSindGleich()
    {
        Gleich([0, 1, 0], Matrix4.DrehungZ(90).Punkt([1, 0, 0]), 1e-12);
        Gleich([-1, 0, 0], Matrix4.DrehungZ(90).Punkt([0, 1, 0]), 1e-12);
        Gleich([1, 2, 3], Matrix4.DrehungZ(0).Punkt([1, 2, 3]));
        Gleich([1, 2, 3], Matrix4.DrehungZ(360).Punkt([1, 2, 3]), 1e-12);
        // Hintereinander: 30° + 60° = 90°
        Gleich(Matrix4.DrehungZ(90).Punkt([5, 1, 0]), (Matrix4.DrehungZ(30) * Matrix4.DrehungZ(60)).Punkt([5, 1, 0]), 1e-12);
    }

    [Fact]
    public void WinkelZ_180Grad_HatBetrag180()
    {
        Assert.Equal(180, Math.Abs(Matrix4.DrehungZ(180).WinkelZ), 9);
    }

    [Fact]
    public void WinkelZ_AusAchsenMitXNachNordOst_Ist45()
    {
        Assert.Equal(45, Matrix4.Achsen([0, 0, 0], [1, 1, 0], null).WinkelZ, 9);
        Assert.Equal(-90, Matrix4.Achsen([0, 0, 0], [0, -1, 0], null).WinkelZ, 9);
    }

    [Fact]
    public void Skalierung_StrecktPunkte_NichtDenUrsprung()
    {
        Gleich([2, 4, 6], Matrix4.Skalierung(2).Punkt([1, 2, 3]));
        Gleich([0, 0, 0], Matrix4.Skalierung(2).Ursprung);
    }

    [Fact]
    public void InverseStarr_HebtDieLageAuf()
    {
        var m = Matrix4.Verschiebung(5, -3, 2) * Matrix4.DrehungZ(30);
        var i = m.InverseStarr();
        var p = new double[] { 11, -7, 4 };
        Gleich(p, i.Punkt(m.Punkt(p)), 1e-9);
        Gleich(p, m.Punkt(i.Punkt(p)), 1e-9);
        var e = m * i;
        for (int r = 0; r < 4; r++)
            for (int c = 0; c < 4; c++)
                Assert.True(Math.Abs((r == c ? 1 : 0) - e[r, c]) < 1e-9, $"M·M⁻¹[{r},{c}] = {e[r, c]}");
    }

    [Fact]
    public void InverseStarr_VonGekippterLage_HebtSieAuf()
    {
        // z-Achse nach Ost, x nach Nord: reine Drehung im Raum, Ursprung (1,2,3)
        var m = Matrix4.Achsen([1, 2, 3], [0, 1, 0], [1, 0, 0]);
        var p = new double[] { 4, 5, 6 };
        Gleich(p, m.InverseStarr().Punkt(m.Punkt(p)), 1e-9);
    }

    [Fact]
    public void InverseStarr_VonVerschiebungUndDrehung_PerHand()
    {
        // M = Verschiebung(10, 0, 0) · Drehung(90°): p → (−y + 10, x, z). Umkehr: q=(a,b,c) → (b, 10 − a, c)
        var m = Matrix4.Verschiebung(10, 0, 0) * Matrix4.DrehungZ(90);
        Gleich([3, 10 - 4, 5], m.InverseStarr().Punkt([4, 3, 5]), 1e-12);
        Gleich([10, 0, 0], m.Ursprung, 1e-12);
        Gleich([0, 10, 0], m.InverseStarr().Ursprung, 1e-12);   // Welt-Ursprung liegt im Modell bei (0, 10)
    }
}
