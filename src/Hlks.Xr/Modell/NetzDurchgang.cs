// Quest App P4a (Modul XR): die Regel «Wände nur unter 500 mm durchscheinend» (N22, Christian 21.10.2026: «bei begehung wände nur transparent wenn man
// durch geht +/- 50cm») auf die Netze der App angewandt. Die Regel selbst steht in Durchgang (begehen-logik.js); hier nur, welche Elemente der GLB
// dazugehören, die Prüfung je Kopfposition und das Aufteilen der Netze in «undurchsichtig» und «im Durchgang».
//
// Bauteile: im Teil «modell» die Ebenen wand und decke (Durchgang.DurchgangEbenen, wie der Prototyp). ANNAHME P4a (Christian zu bestätigen): auch die
// IFC-Unterlage, Entitäten IfcWall*, IfcColumn*, IfcBeam*, IfcSlab*, IfcRoof, IfcCurtainWall* – der Prototyp lässt die Unterlage aus (durchgang3d.js),
// bei Test_praxis steht die Architektur aber nur in der Unterlage; ohne diese Erweiterung könnte man dort nicht durch Wände gehen.
//
// Die Netze sind je Gruppe und Farbe zusammengefasst (P2); ein Element ist ein Dreiecksbereich darin. Kommt ein Element in den Durchgang, nimmt die App seine
// Dreiecke aus dem undurchsichtigen Netz (Aufteilen) und zeichnet sie in einem eigenen, durchscheinenden Netz (Ausschnitt), Deckkraft nach
// Durchgang.DurchgangDeckkraft. Neu gerechnet wird nur, wenn sich die Menge der nahen Elemente ändert.

namespace Hlks.Xr.Modell;

public static class NetzDurchgang
{
    /// <summary>Anfänge der IFC-Entitäten (gross), die in der Unterlage als Wand bzw. Decke gelten (siehe Dateikopf, Annahme).</summary>
    public static readonly IReadOnlyList<string> IfcPraefixe = ["IFCWALL", "IFCCOLUMN", "IFCBEAM", "IFCSLAB", "IFCROOF", "IFCCURTAINWALL"];

    /// <summary>Gehört ein Element mit dieser Gruppe und Ebene bzw. Entität zum Durchgang?</summary>
    public static bool IstBauteil(string gruppe, string? art)
    {
        if (art is null) return false;
        if (gruppe == "architektur") return Durchgang.DurchgangEbenen.Contains(art);
        if (gruppe == "unterlage") return IfcPraefixe.Any(p => art.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        return false;
    }

    /// <summary>
    /// Teilt die Indizes eines Netzes: Uebrig = alle Dreiecke ausser den Bereichen (Dreiecke [Ab, Ab + Anzahl)), Durch = die Dreiecke der Bereiche (in der
    /// Reihenfolge der Bereiche). Bereiche ausserhalb des Netzes werden gekürzt; überlappende zählen einmal.
    /// </summary>
    public static (uint[] Uebrig, uint[] Durch) Aufteilen(uint[] indizes, IEnumerable<(int Ab, int Anzahl)> bereiche)
    {
        int dreiecke = indizes.Length / 3;
        var weg = new bool[dreiecke];
        var durch = new List<uint>();
        foreach (var (ab, anzahl) in bereiche)
            for (int t = Math.Max(0, ab); t < Math.Min(dreiecke, (long)ab + anzahl); t++)
            {
                if (weg[t]) continue;
                weg[t] = true;
                durch.Add(indizes[3 * t]); durch.Add(indizes[3 * t + 1]); durch.Add(indizes[3 * t + 2]);
            }
        var rest = new uint[indizes.Length - durch.Count];
        int j = 0;
        for (int t = 0; t < dreiecke; t++)
        {
            if (weg[t]) continue;
            rest[j++] = indizes[3 * t]; rest[j++] = indizes[3 * t + 1]; rest[j++] = indizes[3 * t + 2];
        }
        return (rest, [.. durch]);
    }

    /// <summary>Kleines Netz aus den Dreiecken (Indizes in das Ausgangsnetz): nur die benutzten Ecken, neu nummeriert.</summary>
    public static (float[] Positionen, float[] Normalen, uint[] Indizes) Ausschnitt(float[] positionen, float[] normalen, uint[] indizes)
    {
        var neu = new Dictionary<uint, uint>();
        var pos = new List<float>();
        var nor = new List<float>();
        var ind = new uint[indizes.Length];
        for (int i = 0; i < indizes.Length; i++)
        {
            var alt = indizes[i];
            if (!neu.TryGetValue(alt, out var n))
            {
                n = (uint)neu.Count;
                neu[alt] = n;
                pos.Add(positionen[3 * alt]); pos.Add(positionen[3 * alt + 1]); pos.Add(positionen[3 * alt + 2]);
                nor.Add(normalen[3 * alt]); nor.Add(normalen[3 * alt + 1]); nor.Add(normalen[3 * alt + 2]);
            }
            ind[i] = n;
        }
        return ([.. pos], [.. nor], ind);
    }
}

/// <summary>Verfolgt die Kopfposition und meldet, welche Bauteile gerade im Durchgang sind (nur neu gerechnet, wenn sich der Kopf bewegt hat).</summary>
public sealed class DurchgangVerfolger(ModellSzene szene, double grenzeMm = Durchgang.DurchgangMm, double bewegungM = 0.005)
{
    readonly Dictionary<string, DreieckForm?> formen = new(StringComparer.Ordinal);
    HashSet<string> nah = new(StringComparer.Ordinal);
    Vec3? letzte;

    /// <summary>Schlüssel der Elemente im Durchgang (SzenenElement.Schluessel).</summary>
    public IReadOnlySet<string> Nah => nah;
    /// <summary>Wie viele Elemente bei der letzten Prüfung genau gemessen wurden (Hüllbox näher als die Grenze).</summary>
    public int Gemessen { get; private set; }

    /// <summary>Prüft für die Kopfposition (Szene, m). true, wenn sich die Menge geändert hat.</summary>
    public bool Pruefen(Vec3 kopf)
    {
        if (!kopf.IstEndlich) return false;
        if (letzte is { } l && l.Abstand(kopf) < bewegungM) return false;
        letzte = kopf;
        var grenze = grenzeMm / 1000;
        var neu = new HashSet<string>(StringComparer.Ordinal);
        int gemessen = 0;
        foreach (var e in szene.DurchgangsBauteile)
        {
            if (e.Box.IstLeer || e.Box.Abstand(kopf) >= grenze) continue;
            gemessen++;
            if (!formen.TryGetValue(e.Schluessel, out var f)) formen[e.Schluessel] = f = DreieckForm.Aus([.. szene.Ecken(e)]);
            if (f is not null && Durchgang.ImDurchgang(kopf, [f], e.Box, grenzeMm)) neu.Add(e.Schluessel);
        }
        Gemessen = gemessen;
        if (neu.SetEquals(nah)) return false;
        nah = neu;
        return true;
    }

    /// <summary>Je Netz die Bereiche der nahen Elemente (für Aufteilen).</summary>
    public Dictionary<int, List<(int Ab, int Anzahl)>> BereicheJeNetz()
    {
        var aus = new Dictionary<int, List<(int, int)>>();
        foreach (var s in nah)
        {
            if (!szene.Elemente.TryGetValue(s, out var e)) continue;
            foreach (var (netz, ab, anzahl) in e.Bereiche)
            {
                if (!aus.TryGetValue(netz, out var l)) aus[netz] = l = [];
                l.Add((ab, anzahl));
            }
        }
        return aus;
    }
}
