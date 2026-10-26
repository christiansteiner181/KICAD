// Quest App P4a, Nachtrag nach dem Brillentest (23.10.2026): Beim Wechsel von Wänden in den Durchgang brauchte ein Bild einmal 24 ms statt 13,9 ms –
// vermutlich, weil der Index-Puffer eines sehr grossen Netzes neu geladen wurde (IFC-Unterlage EG von Test_praxis: 378 000 Dreiecke in einem Netz).
// Die App teilt deshalb grosse Netze beim Aufbau in Blöcke von höchstens BlockDreiecke Dreiecken, möglichst an Grenzen zwischen Elementen (nur ein Element,
// das allein grösser ist, wird geschnitten). Der Durchgang lädt dann nur die Blöcke neu, in denen die nahen Elemente liegen.
// Jeder Block ist ein Zeichenaufruf: für das EG von Test_praxis etwa 20 + 5, klar unter den ~200 von Meta (modul-xr.md, «Prüfung der Technik»).

namespace Hlks.Xr.Modell;

public static class NetzBloecke
{
    /// <summary>Höchstens so viele Dreiecke je Block (Annahme Nachtrag P4a; Stellschraube, wenn die Brille es verlangt).</summary>
    public const int BlockDreiecke = 20_000;

    /// <summary>
    /// Teilt die Dreiecke [0, dreiecke) eines Netzes in Blöcke (Ab, Anzahl) von höchstens <paramref name="max"/> Dreiecken. Geschnitten wird möglichst dort, wo
    /// kein Element-Bereich durchgeht (Lücken zwischen Bereichen und Dreiecke ohne Element zählen als frei). Ein Element, das allein grösser als ein Block ist
    /// (Test_praxis EG: ein IFC-Produkt mit 185 480 Dreiecken), wird in Blöcke zu max geschnitten – der Durchgang rechnet mit Teilbereichen je Block (Lokal),
    /// so bleibt jeder Block klein. Die Blöcke decken alles lückenlos und der Reihe nach ab.
    /// </summary>
    public static IReadOnlyList<(int Ab, int Anzahl)> Teilen(int dreiecke, IReadOnlyList<ElementBereich> bereiche, int max = BlockDreiecke)
    {
        var aus = new List<(int, int)>();
        if (dreiecke <= 0) return aus;
        if (max < 1) max = 1;
        if (dreiecke <= max) { aus.Add((0, dreiecke)); return aus; }
        // erlaubt[p]: zwischen Dreieck p − 1 und p darf geschnitten werden
        var erlaubt = new bool[dreiecke + 1];
        Array.Fill(erlaubt, true);
        foreach (var b in bereiche)
        {
            int von = Math.Max(0, b.Ab), bis = (int)Math.Min(dreiecke, (long)b.Ab + b.Anzahl);
            for (int p = von + 1; p < bis; p++) erlaubt[p] = false;
        }
        int start = 0;
        while (start < dreiecke)
        {
            int schnitt = -1;
            for (int p = Math.Min(dreiecke, start + max); p > start; p--) if (erlaubt[p]) { schnitt = p; break; }
            if (schnitt < 0) schnitt = Math.Min(dreiecke, start + max);   // ein Element grösser als max: in ihm schneiden
            aus.Add((start, schnitt - start));
            start = schnitt;
        }
        return aus;
    }

    /// <summary>Die Bereiche (Dreiecke des Netzes) innerhalb eines Blocks, auf den Block bezogen (Ab ab 0) und auf ihn beschnitten.</summary>
    public static List<(int Ab, int Anzahl)> Lokal(IEnumerable<(int Ab, int Anzahl)> bereiche, int blockAb, int blockAnzahl)
    {
        var aus = new List<(int, int)>();
        foreach (var (ab, anzahl) in bereiche)
        {
            long von = Math.Max(ab, blockAb), bis = Math.Min((long)ab + anzahl, (long)blockAb + blockAnzahl);
            if (bis > von) aus.Add(((int)(von - blockAb), (int)(bis - von)));
        }
        return aus;
    }

    /// <summary>Die Indizes eines Blocks (Dreiecke [ab, ab + anzahl) des Netzes), noch mit den Ecken des ganzen Netzes.</summary>
    public static uint[] Indizes(uint[] indizes, int ab, int anzahl)
    {
        var aus = new uint[anzahl * 3];
        Array.Copy(indizes, (long)ab * 3, aus, 0, (long)anzahl * 3);
        return aus;
    }
}
