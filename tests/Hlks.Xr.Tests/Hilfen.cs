// Gemeinsame Hilfen der Tests (entsprechen den kleinen Funktionen am Anfang der Node-Tests: nah, wd, v).

namespace Hlks.Xr.Tests;

internal static class T
{
    public const double Grad = Math.PI / 180;

    /// <summary>Wie «nah» der Node-Tests: |a − b| &lt; eps.</summary>
    public static void Nah(double a, double b, double eps = 1e-9, string text = "")
        => Assert.True(Math.Abs(a - b) < eps, $"{text} {a} ≠ {b}");

    /// <summary>Winkeldifferenz auf (−π, π].</summary>
    public static double Wd(double a, double b)
    {
        var d = a - b;
        while (d > Math.PI) d -= 2 * Math.PI;
        while (d <= -Math.PI) d += 2 * Math.PI;
        return d;
    }

    public static Vec3 V(double x, double y, double z) => new(x, y, z);

    /// <summary>Wurzel des Repositorys aus dem Pfad der aufrufenden Quelldatei beim Übersetzen (tests/Hlks.Xr.Tests/*.cs) – die Bauausgabe liegt oft ausserhalb.</summary>
    public static string Repo([System.Runtime.CompilerServices.CallerFilePath] string datei = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(datei)!, "..", ".."));

    /// <summary>Gleiche Elemente in gleicher Reihenfolge (wie deepEqual auf Listen).</summary>
    public static void Gleich<TT>(IEnumerable<TT> erwartet, IEnumerable<TT> ist) => Assert.Equal(erwartet.ToArray(), ist.ToArray());

    /// <summary>Antwort der Abfrage raum_begehen (wie in vr.test.mjs bzw. begehen.test.mjs).</summary>
    public static Standort StandortMit(double azimut = 90, double bodenOk = 250)
        => new()
        {
            Raum = "R-1", Name = "Wohnen", Nummer = "0.01", Geschoss = "EG", GeschossName = "Erdgeschoss",
            Standpunkt = new Standpunkt(2000, 1500, 1700, "schwerpunkt"), BodenOk = bodenOk, Augenhoehe = 1700,
            Blick = new Blick(azimut, 0),
        };
}
