// 3D-Schnittbereich: der Teil, den der Sitzungsraum für den Ausschnitt braucht (Raum zeigen (VR), 3D-Ansicht in VR).
// Quelle: wwwroot/js/ansichten/schnittbereich-logik.js (schnittbereichLesen, achsen, ebenen, weltPunkt, ecken, weltGrenzen). Das Ziehen von Griffen und das
// Speichern in der Ansichtseigenschaft gehören nicht zur Quest App und fehlen hier bewusst.
//
// Ein Schnittbereich ist ein Quader { aktiv, min [u, v, z], max [u, v, z], winkel } in Modell-Millimetern. Seine Achsen u, v sind um die Hochachse (Ursprung des
// Modells) um «winkel» (Grad, gegen den Uhrzeigersinn) gegen die Modellachsen x, y gedreht: u = x·cos + y·sin, v = −x·sin + y·cos; z ist absolut.
// Alle Funktionen mit «Welt» rechnen in Szenenmetern (x = Ost, y = oben, z = -Nord).

namespace Hlks.Xr;

/// <summary>Gespeicherter Schnittbereich, wie die Ansichtseigenschaft «schnittbereich» ihn liefert; noch ungeprüft.</summary>
public sealed record SchnittbereichRoh(bool? Aktiv, IReadOnlyList<double>? Min, IReadOnlyList<double>? Max, double? Winkel = null);

/// <summary>Geprüfter Schnittbereich (Kopie): Min und Max je [u, v, z] in mm, Winkel in Grad.</summary>
public sealed record Schnittbereich(bool Aktiv, double[] Min, double[] Max, double Winkel);

/// <summary>
/// Begrenzungsebene für das Clipping: Abstand = Normale · p + Konstante (in m); Punkte mit negativem Abstand werden abgeschnitten, die Normale zeigt ins Innere.
/// </summary>
public sealed record SchnittEbene(string Name, char Achse, int Seite, double[] Normale, double Konstante);

/// <summary>Umschliessende Achsenbox in Szenenmetern.</summary>
public readonly record struct WeltGrenzenBox(Vec3 Min, Vec3 Max);

public static class SchnittbereichLogik
{
    const double M = 1.0 / 1000;
    static readonly string[][] Namen = [["u−", "u+"], ["v−", "v+"], ["z−", "z+"]];
    static readonly char[] AchsenNamen = ['u', 'v', 'z'];

    static bool Dreier(IReadOnlyList<double>? a) => a is { Count: 3 } && a.All(Num.Ok);

    /// <summary>
    /// schnittbereich-logik.js schnittbereichLesen: prüft einen gespeicherten Schnittbereich; null, wenn er fehlt oder ungültig ist
    /// (jede Kante mindestens 1 mm), sonst eine Kopie. «aktiv» gilt, ausser es steht ausdrücklich auf false.
    /// </summary>
    public static Schnittbereich? Lesen(SchnittbereichRoh? roh)
    {
        if (roh is null || !Dreier(roh.Min) || !Dreier(roh.Max)) return null;
        for (var i = 0; i < 3; i++)
            if (!(roh.Max![i] - roh.Min![i] >= 1)) return null;
        return new Schnittbereich(roh.Aktiv != false, roh.Min!.ToArray(), roh.Max!.ToArray(), Num.Ok(roh.Winkel) ? roh.Winkel!.Value : 0);
    }

    /// <summary>schnittbereich-logik.js achsen: Einheitsvektoren der Quaderachsen in Szenenkoordinaten: u, v, z (= oben).</summary>
    public static (double[] U, double[] V, double[] Z) Achsen(double winkelGrad)
    {
        var t = winkelGrad * Num.Grad;
        double c = Math.Cos(t), s = Math.Sin(t);
        return ([c, 0, -s], [-s, 0, -c], [0, 1, 0]);
    }

    /// <summary>
    /// schnittbereich-logik.js ebenen: die sechs Begrenzungsebenen für das Clipping (Reihenfolge u−, u+, v−, v+, z−, z+), Normale und Konstante in Metern.
    /// </summary>
    public static IReadOnlyList<SchnittEbene> Ebenen(Schnittbereich sb)
    {
        var a = Achsen(sb.Winkel);
        double[][] achsen = [a.U, a.V, a.Z];
        var aus = new List<SchnittEbene>();
        for (var i = 0; i < 3; i++)
        {
            var n = achsen[i];
            aus.Add(new SchnittEbene(Namen[i][0], AchsenNamen[i], -1, [.. n], -sb.Min[i] * M));
            aus.Add(new SchnittEbene(Namen[i][1], AchsenNamen[i], 1, n.Select(x => x == 0 ? 0 : -x).ToArray(), sb.Max[i] * M));
        }
        return aus;
    }

    /// <summary>schnittbereich-logik.js weltPunkt: Weltpunkt (m) aus Quaderkoordinaten (u, v, z in mm).</summary>
    public static Vec3 WeltPunkt(Schnittbereich sb, double u, double v, double z)
    {
        var a = Achsen(sb.Winkel);
        double um = u * M, vm = v * M;
        return new Vec3(a.U[0] * um + a.V[0] * vm, z * M, a.U[2] * um + a.V[2] * vm);
    }

    /// <summary>schnittbereich-logik.js ecken: die acht Ecken des Quaders in Szenenkoordinaten (m).</summary>
    public static IReadOnlyList<Vec3> Ecken(Schnittbereich sb)
    {
        var aus = new List<Vec3>();
        foreach (var u in new[] { sb.Min[0], sb.Max[0] })
            foreach (var v in new[] { sb.Min[1], sb.Max[1] })
                foreach (var z in new[] { sb.Min[2], sb.Max[2] })
                    aus.Add(WeltPunkt(sb, u, v, z));
        return aus;
    }

    /// <summary>schnittbereich-logik.js weltGrenzen: umschliessende Achsenbox (Welt) des Quaders in m.</summary>
    public static WeltGrenzenBox WeltGrenzen(Schnittbereich sb)
    {
        var e = Ecken(sb);
        return new WeltGrenzenBox(
            new Vec3(e.Min(p => p.X), e.Min(p => p.Y), e.Min(p => p.Z)),
            new Vec3(e.Max(p => p.X), e.Max(p => p.Y), e.Max(p => p.Z)));
    }
}
