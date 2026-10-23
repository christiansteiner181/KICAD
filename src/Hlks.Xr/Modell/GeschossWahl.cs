// Quest App P4a, Nachtrag nach dem Brillentest (Christian 23.10.2026, Test_praxis): Die App zeichnet nur das Geschoss, in dem man steht, mit seinen
// Bauteilen und seiner IFC-Unterlage («beschränken auf eigenes und IFC»; mit allen Geschossen zeitweise 54–60 Hz statt 72 Hz).
//
// Regel: das oberste Geschoss, dessen Kote höchstens 0,3 m über dem Standboden liegt; liegt der Standboden unter allen, das unterste.
// Standboden = Höhe des Rigs (steht auf OK Boden; ein Sprung behält sie, ein Auftrag setzt sie auf den Boden des Raums), NICHT die Kopfhöhe: im Test liess
// die abgelegte Brille (Kopf 0,37 m über dem Boden) die erste Fassung ins Geschoss darunter wechseln. Die Toleranz von 0,3 m deckt Böden auf, die etwas unter
// der Geschosskote liegen (Bodenaufbau, Absätze); sie ist eine Annahme (Hauptlauf 23.10.2026), keine Norm.
// Koten in KICAD-mm wie in GET /api/xr/geschosse; der Standboden in der Szene wird mit dem Bezugspunkt umgerechnet (StandbodenMm).

using Hlks.Xr.Netz;

namespace Hlks.Xr.Modell;

public static class GeschossWahl
{
    /// <summary>Die Kote darf höchstens so viel über dem Standboden liegen (mm), Annahme.</summary>
    public const double ToleranzMm = 300;

    /// <summary>Standboden in KICAD-mm aus der Höhe des Rigs in der Szene (m) und dem Bezugspunkt.</summary>
    public static double StandbodenMm(double rigY, double[]? bezug) => rigY * 1000 + (bezug is { Length: >= 3 } ? bezug[2] : 0);

    /// <summary>Das Geschoss am Standboden (siehe Dateikopf); null ohne Geschosse. Die Reihenfolge der Liste spielt keine Rolle.</summary>
    public static ListenGeschoss? AmStand(IEnumerable<ListenGeschoss> geschosse, double standbodenMm, double toleranzMm = ToleranzMm)
    {
        var sortiert = geschosse.Where(g => double.IsFinite(g.KoteMm)).OrderBy(g => g.KoteMm).ToList();
        if (sortiert.Count == 0 || !double.IsFinite(standbodenMm)) return sortiert.FirstOrDefault();
        return sortiert.LastOrDefault(g => g.KoteMm <= standbodenMm + toleranzMm) ?? sortiert[0];
    }
}
