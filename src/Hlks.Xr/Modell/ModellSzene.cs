// Quest App P4a (Modul XR): das geladene Modell als Szene – alle GLB (je Geschoss modell und unterlage) zusammen, mit einem Verzeichnis der Elemente
// (Bereiche in den Netzen, Gruppe, Ebene bzw. IFC-Entität, Hüllbox). Ohne StereoKit: die App baut daraus ihre Netze, die Regeln (Durchgang, Räume,
// Startpunkt) arbeiten hier.
//
// Koordinaten: Szene der App (m, y oben, x Ost, z −Nord, relativ zum Bezugspunkt; KicadUmrechnung). Alle GLB eines Stands haben denselben Bezugspunkt
// (P2); weicht einer ab, steht das in den Hinweisen.
// XR-Labor L1: die Szene behält scene.extras.kicad jeder GLB (Extras) – darin bringt der Testraum des Labors die Ziele der Prüfaufgaben («labor»,
// Hlks.Xr.Labor.LaborZiele). Die übrigen Regeln lesen sie nicht.

using System.Text.Json.Nodes;

namespace Hlks.Xr.Modell;

/// <summary>scene.extras.kicad einer GLB der Szene mit Geschoss und Teil.</summary>
public sealed record SzenenExtras(string GeschossId, string Teil, JsonObject Kicad);

/// <summary>Ein Netz der Szene: Nummer, Daten, Geschoss und Teil (modell, unterlage).</summary>
public sealed record SzenenNetz(int Nr, GlbNetzDaten Daten, string GeschossId, string Teil);

/// <summary>Ein Element der Szene (Modell-Element bzw. IFC-Produkt der Unterlage) mit seinen Dreiecksbereichen und seiner Hüllbox.</summary>
public sealed class SzenenElement
{
    /// <summary>Eindeutig in der Szene: «modell:&lt;Id&gt;» bzw. «unterlage:&lt;GlobalId&gt;».</summary>
    public required string Schluessel { get; init; }
    /// <summary>Element-Id (Modell) bzw. GlobalId (Unterlage).</summary>
    public required string Id { get; init; }
    public required string Gruppe { get; init; }
    /// <summary>Ebene (Modell) bzw. IFC-Entität (Unterlage).</summary>
    public string? Art { get; init; }
    public required string GeschossId { get; init; }
    public required string Teil { get; init; }
    public List<(int Netz, int Ab, int Anzahl)> Bereiche { get; } = [];
    public Box3 Box { get; internal set; } = Box3.Leer;
    public int Dreiecke => Bereiche.Sum(b => b.Anzahl);
}

public sealed class ModellSzene
{
    public const string GruppeRaum = "raum";

    readonly List<SzenenNetz> netze = [];
    readonly Dictionary<string, SzenenElement> elemente = new(StringComparer.Ordinal);
    readonly List<SzenenExtras> extras = [];

    public IReadOnlyList<SzenenNetz> Netze => netze;
    /// <summary>XR-Labor L1: scene.extras.kicad je GLB (in der Reihenfolge der Dateien; GLB ohne extras fehlen).</summary>
    public IReadOnlyList<SzenenExtras> Extras => extras;
    public IReadOnlyDictionary<string, SzenenElement> Elemente => elemente;
    public double[] Bezugspunkt { get; } = [0, 0, 0];
    public List<string> Hinweise { get; } = [];
    /// <summary>Hüllbox aller Netze ausser der Räume.</summary>
    public Box3 Box { get; private set; } = Box3.Leer;
    /// <summary>Räume (Gruppe «raum»), für den Startpunkt.</summary>
    public IReadOnlyList<SzenenElement> Raeume { get; }
    /// <summary>Bauteile, die beim Begehen unter 500 mm durchscheinend werden (NetzDurchgang.IstBauteil).</summary>
    public IReadOnlyList<SzenenElement> DurchgangsBauteile { get; }

    public ModellSzene(IEnumerable<(GlbDatei Datei, string GeschossId, string Teil)> dateien)
    {
        bool erster = true;
        foreach (var (datei, geschoss, teil) in dateien)
        {
            if (erster) { Array.Copy(datei.Bezugspunkt, Bezugspunkt, 3); erster = false; }
            else if (!datei.Bezugspunkt.SequenceEqual(Bezugspunkt))
                Hinweise.Add($"Geschoss {datei.GeschossKennzeichen ?? geschoss} ({teil}): anderer Bezugspunkt [{string.Join(", ", datei.Bezugspunkt)}] als die übrigen GLB.");
            if (datei.Kicad is { } k) extras.Add(new SzenenExtras(geschoss, teil, k));
            foreach (var n in datei.Netze)
            {
                var sn = new SzenenNetz(netze.Count, n, geschoss, teil);
                netze.Add(sn);
                foreach (var b in n.Bereiche)
                {
                    var schluessel = $"{teil}:{b.Id}";
                    if (!elemente.TryGetValue(schluessel, out var e))
                        elemente[schluessel] = e = new SzenenElement { Schluessel = schluessel, Id = b.Id, Gruppe = n.Gruppe, Art = b.Art, GeschossId = geschoss, Teil = teil };
                    e.Bereiche.Add((sn.Nr, b.Ab, b.Anzahl));
                }
            }
        }
        // Hüllboxen: der Elemente aus ihren Dreiecken, der Szene aus allen Ecken ausser den Räumen
        foreach (var e in elemente.Values)
        {
            var box = Box3.Leer;
            foreach (var p in Ecken(e)) box = box.Plus(p);
            e.Box = box;
        }
        var gesamt = Box3.Leer;
        foreach (var sn in netze.Where(x => x.Daten.Gruppe != GruppeRaum))
        {
            var pos = sn.Daten.Positionen;
            for (int i = 0; i + 2 < pos.Length; i += 3) gesamt = gesamt.Plus(new Vec3(pos[i], pos[i + 1], pos[i + 2]));
        }
        Box = gesamt;
        Raeume = [.. elemente.Values.Where(e => e.Gruppe == GruppeRaum).OrderBy(e => e.Schluessel, StringComparer.Ordinal)];
        DurchgangsBauteile = [.. elemente.Values.Where(e => NetzDurchgang.IstBauteil(e.Gruppe, e.Art)).OrderBy(e => e.Schluessel, StringComparer.Ordinal)];
    }

    /// <summary>Liest die GLB eines Modellstands und baut die Szene (Datei, Geschoss-Id, Teil). GLB, die sich nicht lesen lassen, stehen in den Hinweisen.</summary>
    public static ModellSzene AusGlb(IEnumerable<(byte[] Glb, string GeschossId, string Teil, string Name)> teile)
    {
        var gelesen = new List<(GlbDatei, string, string)>();
        var fehler = new List<string>();
        foreach (var (glb, g, t, name) in teile)
        {
            try { gelesen.Add((GlbLeser.Lesen(glb), g, t)); }
            catch (GlbFehler f) { fehler.Add($"{name}: {f.Message}"); }
        }
        var s = new ModellSzene(gelesen);
        s.Hinweise.AddRange(fehler);
        return s;
    }

    /// <summary>Die Ecken der Dreiecke eines Elements (je drei hintereinander), in der Szene.</summary>
    public IEnumerable<Vec3> Ecken(SzenenElement e)
    {
        foreach (var (nr, ab, anzahl) in e.Bereiche)
        {
            var d = netze[nr].Daten;
            for (int t = ab; t < ab + anzahl; t++)
                for (int k = 0; k < 3; k++) yield return d.Ecke(d.Indizes[3 * t + k]);
        }
    }

    /// <summary>Netze und Dreiecke, die gezeichnet werden (ohne die ausgeblendeten Gruppen, z. B. «raum»).</summary>
    public (int Netze, long Dreiecke) Sichtbar(IReadOnlySet<string> ausgeblendet)
    {
        var s = netze.Where(n => !ausgeblendet.Contains(n.Daten.Gruppe) && n.Daten.Dreiecke > 0).ToList();
        return (s.Count, s.Sum(n => (long)n.Daten.Dreiecke));
    }

    /// <summary>Element nach Id bzw. GlobalId (Modell zuerst), sonst null.</summary>
    public SzenenElement? Finde(string id) =>
        elemente.TryGetValue("modell:" + id, out var e) ? e : elemente.TryGetValue("unterlage:" + id, out var u) ? u : null;
}
