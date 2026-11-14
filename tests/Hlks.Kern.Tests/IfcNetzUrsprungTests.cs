using Xbim.Ifc4.Interfaces;
namespace Hlks.Kern.Tests;

/// <summary>Netzdatei Fassung 6 (Gesamtprüfung Frage 32 «Bezugspunkt»): Punkte als float32 relativ zu einem Ursprung im Kopf (float64). Absolut in float32
/// wäre eine Wand bei Landeskoordinaten auf 256 mm genau (24 Bit Mantisse bei x ≈ 2.6e9 mm) – die 200 mm Wanddicke ginge verloren. Datei FIKTIV.</summary>
public class IfcNetzUrsprungTests
{
    [Fact]
    public void Fassung6_WandBeiLandeskoordinaten_AufBruchteileEinesMillimetersGenau()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "hlks-netz6-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var pfad = IfcLageTestdatei.Schreiben("IFC4", Path.Combine(ordner, "lv95.ifc"), new LageSpec { Gelaende = new Pl(2_600_123_400, 1_200_456_700, 500_000) });
            using var d = IfcDatei.Oeffnen(pfad);
            var waende = d.Modell.Instances.OfType<IIfcProduct>().Where(p => p is IIfcWall).ToList();
            var ziel = Path.Combine(ordner, "lv95.netz");
            new IfcNetzErzeuger().Erzeugen(d, waende, new HashSet<int>(), ziel);
            var n = IfcNetzdatei.Oeffnen(ziel);
            Assert.Equal(6, n.Fassung);
            Assert.True(n.Ursprung[0] > 2.5e9 && n.Ursprung[1] > 1.1e9, string.Join(", ", n.Ursprung));   // Ursprung im Gebäude, nicht 0
            var w = Assert.Single(n.Produkte);
            var bb = w.Begrenzung;
            var masse = new[] { bb[3] - bb[0], bb[4] - bb[1], bb[5] - bb[2] }.OrderBy(x => x).ToArray();
            Assert.Equal(200, masse[0], 2);    // Wanddicke (LageSpec: 5000 × 200 × 2600)
            Assert.Equal(2600, masse[1], 2);
            Assert.Equal(5000, masse[2], 2);
            Assert.InRange(bb[0], 2_600_123_400 - 10_000.0, 2_600_123_400 + 10_000.0);   // absolute Projekt-mm zurück
            var (pu, _) = n.Netz(w);
            Assert.Equal(bb[0], Enumerable.Range(0, pu.Length / 3).Min(i => pu[3 * i]), 3);
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } }
    }
}
