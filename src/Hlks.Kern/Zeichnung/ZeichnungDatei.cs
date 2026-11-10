// 2D-CAD «Zeichnung» (entwurf/modul-2d-cad.md), Stufe 1: eine geöffnete DWG/DXF-Datei als vollständiges Dokument.
// Anders als die Unterlagen (CadLeser zerlegt in Primitive) bleibt hier das ganze Dokument von ACadSharp im Speicher – Layer,
// Stile, Blöcke, Layouts. Was der Leser beim Öffnen meldet (unbekannte Objekte), steht im Bericht und geht nie still verloren.
// ZeichnungSitzung hält die geöffneten Zeichnungen eines Laufs (MCP-Server, später Oberfläche) und prüft, dass nur Dateien
// aus den freigegebenen Ordnern geöffnet werden.

using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;
using ACadSharp.Tables;

namespace Hlks.Kern;

public sealed class Zeichnungsdatei
{
    /// <summary>Obergrenze der Dateigrösse beim Öffnen (Schutz des Arbeitsspeichers).</summary>
    public const long MaxBytes = 300L * 1024 * 1024;

    public string Name { get; private set; }
    public string Pfad { get; private set; }
    /// <summary>Rückgängig/Wiederholen der Sitzung (Stufe 2, ZeichnungBearbeiten.cs).</summary>
    public ZeichnungJournal Journal { get; } = new();
    /// <summary>Seit dem Öffnen bzw. letzten Speichern geändert.</summary>
    public bool Geaendert => Journal.Stand != gespeicherterStand;
    int gespeicherterStand;

    /// <summary>Nach dem Speichern: Stand merken, bei «Speichern unter» den neuen Pfad übernehmen.</summary>
    internal void Gespeichert(string pfad)
    {
        gespeicherterStand = Journal.Stand;
        Pfad = pfad;
        Name = Path.GetFileName(pfad);
    }
    public CadDocument Dokument { get; }
    /// <summary>Meldungen des Lesers, zusammengefasst (Text → Anzahl).</summary>
    public SortedDictionary<string, int> Meldungen { get; } = new(StringComparer.Ordinal);
    public double MmJeEinheit { get; }
    public bool EinheitBekannt { get; }

    Zeichnungsdatei(string pfad, CadDocument dok)
    {
        Pfad = pfad;
        Name = Path.GetFileName(pfad);
        Dokument = dok;
        (MmJeEinheit, EinheitBekannt) = CadLeser.MmJe(dok.Header.InsUnits);
    }

    public static Zeichnungsdatei Oeffnen(string pfad)
    {
        var info = new FileInfo(pfad);
        if (!info.Exists) throw new HlksFehler($"Die Datei «{pfad}» gibt es nicht.", "Pfad prüfen; mit zeichnungen die Dateien der freigegebenen Ordner auflisten.");
        if (info.Length > MaxBytes) throw new HlksFehler($"«{info.Name}» ist mit {info.Length / 1024 / 1024} MB zu gross (Grenze {MaxBytes / 1024 / 1024} MB).");
        bool dxf = pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase);
        var meldungen = new List<string>();
        void Notiz(object? s, NotificationEventArgs e) { if (e.NotificationType != NotificationType.None) lock (meldungen) meldungen.Add(e.Message); }
        CadDocument dok;
        try { dok = dxf ? DxfReader.Read(pfad, Notiz) : DwgReader.Read(pfad, Notiz); }
        catch (Exception ex) when (ex is not HlksFehler and not OutOfMemoryException)
        {
            throw new HlksFehler($"Die {(dxf ? "DXF" : "DWG")}-Datei «{info.Name}» lässt sich nicht lesen: {ex.Message}",
                dxf ? "Die Datei als DXF (ASCII oder binär, R12 oder neuer) neu speichern."
                    : "Gelesen werden DWG R14 bis 2018 und neuer (AC1014–AC1032); ältere Dateien als DWG 2000 oder neuer speichern.");
        }
        var z = new Zeichnungsdatei(info.FullName, dok);
        foreach (var m in meldungen)
        {
            var kurz = m.Length > 160 ? m[..160] + "…" : m;
            z.Meldungen[kurz] = z.Meldungen.GetValueOrDefault(kurz) + 1;
        }
        return z;
    }

    // ---------- Layouts ----------

    /// <summary>Namen der Layouts: «Model» zuerst, dann die Papierbereiche in Reihenfolge der Register.</summary>
    public IReadOnlyList<string> Layouts()
    {
        var papier = Dokument.Layouts.Where(l => l.IsPaperSpace).OrderBy(l => l.TabOrder).Select(l => l.Name);
        return ["Model", .. papier];
    }

    /// <summary>Layout nach Name; null, leer, «Model» oder «Modell» = Modellbereich (Rückgabe null).</summary>
    public ACadSharp.Objects.Layout? Layout(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Equals("Model", StringComparison.OrdinalIgnoreCase) || name.Equals("Modell", StringComparison.OrdinalIgnoreCase)) return null;
        return Dokument.Layouts.FirstOrDefault(l => l.IsPaperSpace && l.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"Das Layout «{name}» gibt es in {Name} nicht.", "Vorhanden: " + string.Join(", ", Layouts()));
    }

    /// <summary>Die Objekte eines Layouts in Zeichenreihenfolge (Modellbereich bei null).</summary>
    public IEnumerable<Entity> Objekte(ACadSharp.Objects.Layout? layout)
    {
        var block = layout?.AssociatedBlock ?? Dokument.ModelSpace;
        try { return block.GetSortedEntities().ToList(); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return block.Entities; }   // fehlerhafte Sortiertabelle: Dateireihenfolge
    }

    // ---------- Layer ----------

    public static bool Sichtbar(Layer l) => l.IsOn && !l.Flags.HasFlag(LayerFlags.Frozen);

    /// <summary>Layer nach Namen bzw. Muster mit * (Gross-/Kleinschreibung egal).</summary>
    public List<Layer> LayerSuchen(IEnumerable<string> muster)
    {
        var treffer = new List<Layer>();
        foreach (var m in muster.Where(m => !string.IsNullOrWhiteSpace(m)))
        {
            var gefunden = Dokument.Layers.Where(l => Passt(l.Name, m.Trim())).ToList();
            if (gefunden.Count == 0) throw new HlksFehler($"Kein Layer passt auf «{m}».", "Layer mit zeichnung_layer auflisten; * steht für beliebige Zeichen.");
            treffer.AddRange(gefunden.Where(l => !treffer.Contains(l)));
        }
        return treffer;
    }

    /// <summary>Vergleich mit * als Platzhalter für beliebig viele Zeichen.</summary>
    public static bool Passt(string text, string muster)
    {
        if (!muster.Contains('*')) return text.Equals(muster, StringComparison.OrdinalIgnoreCase);
        var teile = muster.Split('*');
        int pos = 0;
        for (int i = 0; i < teile.Length; i++)
        {
            var t = teile[i];
            if (t.Length == 0) continue;
            int f = text.IndexOf(t, pos, StringComparison.OrdinalIgnoreCase);
            if (f < 0 || (i == 0 && f != 0)) return false;
            pos = f + t.Length;
        }
        return teile[^1].Length == 0 || text.EndsWith(teile[^1], StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Kennung eines Objekts: Handle der DWG als Hexzahl (bleibt über das Speichern erhalten).</summary>
    public static string Kennung(CadObject o) => o.Handle.ToString("X");

    public Entity Objekt(string kennung)
    {
        if (ulong.TryParse(kennung.Trim(), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var h)
            && Dokument.TryGetCadObject<Entity>(h, out var e) && e is not null) return e;
        throw new HlksFehler($"Das Objekt «{kennung}» gibt es in {Name} nicht.", "Kennungen liefert zeichnung_objekte (Feld kennung).");
    }
}

/// <summary>Geöffnete Zeichnungen eines Laufs und die Ordner, aus denen geöffnet werden darf.</summary>
public sealed class ZeichnungSitzung
{
    readonly List<string> ordner = [];
    readonly Dictionary<string, Zeichnungsdatei> offen = new(StringComparer.OrdinalIgnoreCase);
    string? zuletzt;

    readonly bool ohneGrenze;

    /// <param name="ohneGrenze">true: jede Datei dieses Rechners darf geöffnet werden – nur für den Arbeitsplatz des Menschen
    /// (Oberfläche auf localhost), nie für die KI (MCP: dort gelten die freigegebenen Ordner).</param>
    public ZeichnungSitzung(IEnumerable<string> freigegeben, bool ohneGrenze = false)
    {
        this.ohneGrenze = ohneGrenze;
        foreach (var o in freigegeben.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            var voll = Path.TrimEndingDirectorySeparator(Path.GetFullPath(o));
            if (!ordner.Contains(voll, StringComparer.OrdinalIgnoreCase)) ordner.Add(voll);
        }
    }

    public IReadOnlyList<string> Ordner => ordner;
    public IReadOnlyCollection<Zeichnungsdatei> Offen => offen.Values;

    static bool IstZeichnung(string pfad) => pfad.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase) || pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase);

    /// <summary>Voller Pfad einer Datei, wenn sie in einem freigegebenen Ordner liegt (sonst Fehler). Relative Angaben gelten ab den Ordnern.</summary>
    public string Pruefen(string datei)
    {
        if (ohneGrenze)
        {
            if (string.IsNullOrWhiteSpace(datei) || !Path.IsPathRooted(datei)) throw new HlksFehler("Der Pfad der Zeichnung fehlt.", "Den vollen Pfad einer DWG- oder DXF-Datei angeben.");
            if (!IstZeichnung(datei)) throw new HlksFehler($"«{Path.GetFileName(datei)}» ist keine DWG- oder DXF-Datei.", "Geöffnet werden .dwg und .dxf.");
            var voll = Path.GetFullPath(datei);
            return File.Exists(voll) ? voll : throw new HlksFehler($"Die Datei «{datei}» gibt es nicht.", "Pfad prüfen (Netzlaufwerk verbunden?).");
        }
        if (ordner.Count == 0)
            throw new HlksFehler("Es ist kein Ordner für Zeichnungen freigegeben.", "Den MCP-Server mit --zeichnungen <Ordner> [<Ordner> …] starten.");
        if (string.IsNullOrWhiteSpace(datei)) throw new HlksFehler("datei fehlt.", "Dateiname oder Pfad einer DWG/DXF-Datei angeben.");
        if (!IstZeichnung(datei)) throw new HlksFehler($"«{Path.GetFileName(datei)}» ist keine DWG- oder DXF-Datei.", "Geöffnet werden .dwg und .dxf.");
        var kandidaten = Path.IsPathRooted(datei) ? [Path.GetFullPath(datei)] : ordner.Select(o => Path.GetFullPath(Path.Combine(o, datei))).ToList();
        var erlaubt = kandidaten.Where(Innerhalb).ToList();
        if (erlaubt.Count == 0)
            throw new HlksFehler($"«{datei}» liegt ausserhalb der freigegebenen Ordner.", "Freigegeben: " + string.Join(" · ", ordner));
        return erlaubt.FirstOrDefault(File.Exists)
            ?? throw new HlksFehler($"Die Datei «{datei}» gibt es in den freigegebenen Ordnern nicht.", "Mit zeichnungen die vorhandenen Dateien auflisten.");
    }

    /// <summary>Voller Pfad einer Zieldatei zum Speichern (.dwg/.dxf, muss noch nicht bestehen): nur in den freigegebenen Ordnern.</summary>
    public string ZielPruefen(string datei)
    {
        if (!IstZeichnung(datei)) throw new HlksFehler("Die Zieldatei muss auf .dwg oder .dxf enden.");
        if (ohneGrenze) return Path.IsPathRooted(datei) ? Path.GetFullPath(datei) : throw new HlksFehler("Der Pfad der Zieldatei fehlt.", "Den vollen Pfad angeben.");
        if (ordner.Count == 0) throw new HlksFehler("Es ist kein Ordner für Zeichnungen freigegeben.", "Den MCP-Server mit --zeichnungen <Ordner> starten.");
        var voll = Path.GetFullPath(Path.IsPathRooted(datei) ? datei : Path.Combine(ordner[0], datei));
        return Innerhalb(voll) ? voll : throw new HlksFehler($"«{datei}» liegt ausserhalb der freigegebenen Ordner.", "Freigegeben: " + string.Join(" · ", ordner));
    }

    bool Innerhalb(string voll)
    {
        foreach (var o in ordner)
        {
            if (!voll.StartsWith(o + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) continue;
            // Verknüpfungen, die aus dem Ordner hinausführen, zählen nicht
            try
            {
                var ziel = File.ResolveLinkTarget(voll, true)?.FullName;
                if (ziel is not null && !ordner.Any(x => ziel.StartsWith(x + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) return false;
            }
            catch (IOException) { }
            return true;
        }
        return false;
    }

    /// <summary>DWG/DXF-Dateien der freigegebenen Ordner (mit Unterordnern), höchstens <paramref name="max"/>.</summary>
    public List<(string Pfad, long Bytes, DateTime Geaendert)> Dateien(string? filter, int max, out bool mehr)
    {
        var liste = new List<(string, long, DateTime)>();
        mehr = false;
        var opt = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8 };
        foreach (var o in ordner.Where(Directory.Exists))
            foreach (var f in Directory.EnumerateFiles(o, "*.d??", opt))
            {
                if (!IstZeichnung(f) || (filter is { Length: > 0 } && !Path.GetFileName(f).Contains(filter, StringComparison.OrdinalIgnoreCase))) continue;
                if (liste.Count >= max) { mehr = true; return liste; }
                var i = new FileInfo(f);
                liste.Add((f, i.Length, i.LastWriteTime));
            }
        return liste;
    }

    /// <summary>Neue, leere Zeichnung (Millimeter, DWG 2018 bzw. DXF) anlegen und öffnen; eine bestehende Datei wird nie überschrieben.</summary>
    public Zeichnungsdatei Anlegen(string datei)
    {
        var pfad = ZielPruefen(datei);
        if (File.Exists(pfad)) throw new HlksFehler($"Die Datei «{Path.GetFileName(pfad)}» gibt es schon.", "Einen anderen Namen wählen oder die Datei öffnen.");
        var doc = new CadDocument(ACadVersion.AC1032);
        doc.Header.InsUnits = ACadSharp.Types.Units.UnitsType.Millimeters;
        // Bemassungsstil metrisch (die Vorgabe der Bibliothek ist Zoll: Text 0.18): Papier-mm wie die ISO-Vorlage von AutoCAD
        foreach (var st in doc.DimensionStyles) { st.TextHeight = 2.5; st.ArrowSize = 2.5; st.ExtensionLineOffset = 0.625; st.ExtensionLineExtension = 1.25; st.DimensionLineGap = 0.625; }
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        if (pfad.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase)) { using var w = new DxfWriter(pfad, doc, false); w.Write(); }
        else { using var w = new DwgWriter(pfad, doc); w.Write(); }
        return Oeffnen(pfad);
    }

    public Zeichnungsdatei Oeffnen(string datei)
    {
        var pfad = Pruefen(datei);
        var z = Zeichnungsdatei.Oeffnen(pfad);
        var schluessel = Schluessel(z);
        offen[schluessel] = z;
        zuletzt = schluessel;
        return z;
    }

    /// <summary>Name, unter dem die Zeichnung in der Sitzung geführt wird: Dateiname, bei gleichem Namen aus anderem Ordner der volle Pfad.</summary>
    string Schluessel(Zeichnungsdatei z) =>
        offen.TryGetValue(z.Name, out var da) && !da.Pfad.Equals(z.Pfad, StringComparison.OrdinalIgnoreCase) ? z.Pfad : z.Name;

    /// <summary>Geöffnete Zeichnung nach Name oder Pfad; ohne Angabe die zuletzt geöffnete.</summary>
    public Zeichnungsdatei Holen(string? zeichnung)
    {
        if (offen.Count == 0) throw new HlksFehler("Es ist keine Zeichnung geöffnet.", "Zuerst zeichnung_oeffnen mit datei aufrufen.");
        if (string.IsNullOrWhiteSpace(zeichnung)) return offen[zuletzt!];
        if (offen.TryGetValue(zeichnung, out var z)) return z;
        return offen.Values.FirstOrDefault(x => x.Pfad.Equals(zeichnung, StringComparison.OrdinalIgnoreCase) || x.Name.Equals(Path.GetFileName(zeichnung), StringComparison.OrdinalIgnoreCase))
            ?? throw new HlksFehler($"Die Zeichnung «{zeichnung}» ist nicht geöffnet.", "Geöffnet: " + string.Join(", ", offen.Keys));
    }

    public bool Schliessen(string? zeichnung)
    {
        var z = Holen(zeichnung);
        var k = offen.First(x => ReferenceEquals(x.Value, z)).Key;
        offen.Remove(k);
        if (zuletzt == k) zuletzt = offen.Keys.LastOrDefault();
        return true;
    }
}
