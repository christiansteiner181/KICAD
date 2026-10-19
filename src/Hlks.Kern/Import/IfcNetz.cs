// Netzdatei einer IFC-Unterlage (Modul 1b, B1; Fassung 2 seit Modul 3b, B1b): die exakte Geometrie aller Produkte als
// Dreiecksnetze in Projektkoordinaten – auch der übernommenen (Kennzeichen «übernommen»), als Grundlage für Kollisionsprüfung
// und automatische Aussparungen (Modul 3b). Die nicht übernommenen bilden den Hintergrund (Grundriss-Schnitt, 3D).
// Christian 17.10.2026: «Revit behandelt dies wie ein PDF … noch eine korrekte Kollisionsprüfung durchführen» – darum
// bleibt jedes Produkt mit seiner Geometrie und GlobalId erhalten.
//
// Format (binär, little-endian; dieselbe Beschreibung steht im Dateikopf):
//   8 Byte «HLKSNETZ», int32 Fassung (2), int32 Länge L, L Byte Beschreibung (UTF-8),
//   int32 Anzahl Datensätze N (Bauteile, Teile und Öffnungen), 6 × float64 Begrenzung aller Bauteile und Teile (minx, miny,
//   minz, maxx, maxy, maxz, Projekt-mm),
//   je Datensatz: int32 EntityLabel, 22 Byte GlobalId (ASCII), uint8 n + n Byte Entität (ASCII), float64 Geschosskote
//   (mm, NaN = ohne Geschoss), uint8 Kennzeichen (1 = übernommen, 0 = Hintergrund),
//   [nur Fassung 2:] uint8 Rolle (0 = bauteil, 1 = teil, 2 = oeffnung), 22 Byte Bezug (ASCII; teil: GlobalId des Ganzen,
//   oeffnung: GlobalId des Wirts; sonst 22 Leerzeichen), uint8 f + f × 22 Byte GlobalIds der Füllungen (oeffnung: Tür,
//   Fenster …; sonst f = 0),
//   [nur Fassung 3:] uint8 m + m × (uint16 L + L Byte UTF-8) Materialnamen, uint16 L + L Byte UTF-8 Typname, uint16 L + L Byte
//   UTF-8 Fundstelle des Materials (L = 0: keine Angabe),
//   6 × float32 Begrenzung, int32 Anzahl Punkte p, int32 Anzahl Dreiecke d, p × 3 float32 Punkte (x, y, z in Projekt-mm),
//   d × 3 int32 Dreiecke (Punktindizes 0-basiert, je Datensatz).
// Fassung 1 (Modul 1b) hat die drei Felder der Fassung 2 nicht und keine Öffnungen; gelesen wird sie weiter (alle Produkte gelten
// als Rolle bauteil ohne Bezug). Fassungen 1 und 2 haben kein Material (Leser: leere Liste, Typname null).
//
// Fassung 3 (Nachtrag B3c, Entscheid Christian 19.10.2026 «Aussparungen nur in Beton und Mauerwerk»): je Produkt das Material aus
//   IfcRelAssociatesMaterial – am Exemplar, sonst am Typobjekt (IfcRelDefinesByType) – als Liste der Namen: IfcMaterial (IFC4 mit
//   Category in Klammern, wenn sie etwas anderes sagt), IfcMaterialList, IfcMaterialLayerSetUsage / IfcMaterialLayerSet /
//   IfcMaterialLayer (Schichten in ihrer Folge), IfcMaterialProfileSetUsage / IfcMaterialProfileSet, IfcMaterialConstituentSet
//   (nur IFC4). Geprüft an den Xbim-Klassen (Test AussparungMaterialTests.MaterialSelect_AmXbimSchema): IFC2X3 kennt IfcMaterial,
//   IfcMaterialList, IfcMaterialLayerSetUsage, IfcMaterialLayerSet, IfcMaterialLayer; IFC4 zusätzlich die Profil- und
//   Konstituenten-Mengen. Ein Ganzes ohne eigenen Körper ohne eigenes Material trägt die Materialien seiner Teile. Dazu der
//   Typname (ObjectType des Exemplars, sonst Name des Typobjekts) – Befund Bernstrasse 42: der Revit-Export der verknüpften
//   Architektur führt als Material nur Darstellungsmaterialien («Farbe, weiss», «<Unnamed>»), die Bauart steht im Typnamen
//   («Beton, Stahlbeton 250», «1.0_AW Holz …»). Die Einteilung (Wortliste) macht AussparungMaterial.cs.
//
// Regeln (Fassung 2, Entscheid Hauptlauf 19.10.2026, Befunde aus Modul 3b B1):
//   Zerlegung: Ein Produkt ist ein Teil, wenn es in IfcRelAggregates oder IfcRelNests (IFC4 und IFC2x3: RelatingObject →
//     RelatedObjects, am Schema der Xbim-Klassen geprüft) unter einem IfcElement steht (Schichten IfcBuildingElementPart,
//     Platten eines Dachs, Läufe einer Treppe …).
//   Ganzes mit eigenem Körper: es steht mit seinem Netz darin, die Teile zusätzlich als Rolle teil mit der GlobalId des Ganzen
//     (wer prüft, zählt sie nicht doppelt).
//   Ganzes ohne eigenen Körper (kein Netz, auch ohne Repräsentation – Archicad-Decken aus Teilen): es steht mit dem Netz aller
//     Teile unter seiner GlobalId darin (auch wenn es selbst nicht in der übergebenen Liste steht); die Teile erscheinen nicht
//     einzeln. Ausnahme: ein Teil, das selbst übernommen ist, während das Ganze es nicht ist (Platten eines Dachs ohne Körper,
//     je Platte eine Decke), steht einzeln als Rolle teil darin – sonst trügen das Ganze im Hintergrund und die Elemente
//     dieselbe Geometrie doppelt.
//   Öffnungen: jede IfcOpeningElement (über IfcRelVoidsElement), deren Wirt in der Datei steht, als eigener Datensatz mit
//     ihrem Netz (Repräsentation Body, sonst Reference), Bezug = GlobalId des Wirts (ist der Wirt ein Teil, das im Netz seines
//     Ganzen aufgeht: das Ganze), Füllungen über IfcRelFillsElement. Öffnungen ganz ohne IfcRelVoidsElement (Befund
//     Bernstrasse 42: der Revit-Export der verknüpften Architektur hat 152 Öffnungen und 3676 Teile ohne jede Beziehung) stehen
//     mit leerem Bezug darin, wenn sie im Bereich der geschriebenen Bauteile liegen; den Wirt bestimmt dann der Leser aus der
//     Lage. Die Bauteilnetze bleiben, wie sie sind (kein boolesches Abziehen); wer prüft, zieht die Öffnungen logisch ab.
//     Öffnungen sind nie übernommen (Kennzeichen 0) und gehören nicht zum Hintergrund: Leser zeichnen sie nicht
//     (IfcNetzdatei.Produkte und Alle liefern sie nicht, nur Oeffnungen).

using System.Text;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Rolle eines Datensatzes der Netzdatei (Fassung 2; Fassung 1 kennt nur Bauteil).</summary>
public enum IfcNetzRolle : byte { Bauteil = 0, Teil = 1, Oeffnung = 2 }

/// <summary>Ein Produkt der Netzdatei (Kopfdaten; die Dreiecke liest IfcNetzdatei.Netz).</summary>
public sealed record IfcNetzProdukt(int EntityLabel, string GlobalId, string Entitaet, double? GeschossKote, bool Uebernommen,
    float[] Begrenzung, int Punkte, int Dreiecke, long Lage)
{
    /// <summary>bauteil | teil | oeffnung (Fassung 1: immer bauteil).</summary>
    public IfcNetzRolle Rolle { get; init; } = IfcNetzRolle.Bauteil;
    /// <summary>Teil: GlobalId des Ganzen; Öffnung: GlobalId des Wirts; sonst null.</summary>
    public string? Bezug { get; init; }
    /// <summary>Öffnung: GlobalIds der Füllungen (IfcRelFillsElement – Tür, Fenster); sonst leer.</summary>
    public IReadOnlyList<string> Fuellungen { get; init; } = [];
    /// <summary>Öffnung mit Füllung (Tür, Fenster)?</summary>
    public bool Gefuellt => Fuellungen.Count > 0;
    /// <summary>Fassung 3: Materialnamen aus IfcRelAssociatesMaterial (Exemplar, sonst Typobjekt; Ganzes ohne Körper: seine Teile);
    /// Fassungen 1 und 2: leer.</summary>
    public IReadOnlyList<string> Materialien { get; init; } = [];
    /// <summary>Fassung 3: ObjectType des Exemplars, sonst Name des Typobjekts; sonst null.</summary>
    public string? Typname { get; init; }
    /// <summary>Fassung 3: Fundstelle des Materials, z. B. «IfcMaterialList #123 am Typ #45»; sonst null.</summary>
    public string? Materialquelle { get; init; }
}

/// <summary>Schreibt die Netzdatei (IIfcGrundlage, von IfcUebernehmen per Reflexion gefunden).</summary>
public sealed class IfcNetzErzeuger : IIfcGrundlage
{
    public const string Kennung = "HLKSNETZ";
    /// <summary>Fassung, die geschrieben wird (2: mit Rolle, Bezug und Öffnungen; 3: dazu Material und Typname, Nachtrag B3c).</summary>
    public const int Fassung = 3;
    /// <summary>Erste Fassung mit Material und Typname je Produkt.</summary>
    public const int FassungMaterial = 3;
    /// <summary>Fassungen, die IfcNetzdatei lesen kann.</summary>
    public static readonly IReadOnlyList<int> LesbareFassungen = [1, 2, 3];
    /// <summary>Höchstens so viele Zeichen je Text (Materialname, Typname, Fundstelle) – Schutz gegen entartete Dateien.</summary>
    public const int TextMax = 1000;
    /// <summary>Länge einer GlobalId (IFC: 22 Zeichen, base64-ähnlich).</summary>
    public const int GidLaenge = 22;

    public const string Beschreibung =
        "HLKS-CAD Netzdatei einer IFC-Unterlage (Modul 1b, Fassung 2 seit Modul 3b, Fassung 3 seit Nachtrag B3c). Little-endian. Kopf: 8 Byte HLKSNETZ, int32 Fassung, " +
        "int32 Laenge L, L Byte diese Beschreibung (UTF-8), int32 Anzahl Datensaetze N (Bauteile, Teile, Oeffnungen), 6 float64 Begrenzung " +
        "der Bauteile und Teile (minx miny minz maxx maxy maxz, Projekt-mm). Je Datensatz: int32 EntityLabel, 22 Byte GlobalId (ASCII), " +
        "uint8 n + n Byte Entitaet (ASCII), float64 Geschosskote mm (NaN = ohne), uint8 Kennzeichen (1 = uebernommen als KICAD-Element, " +
        "0 = Hintergrund), uint8 Rolle (0 = bauteil, 1 = teil, 2 = oeffnung), 22 Byte Bezug (ASCII; teil: GlobalId des Ganzen, " +
        "oeffnung: GlobalId des Wirts, sonst Leerzeichen), uint8 f + f*22 Byte GlobalIds der Fuellungen (oeffnung: Tuer, Fenster; " +
        "IfcRelFillsElement), uint8 m + m*(uint16 L + L Byte UTF-8) Materialnamen (IfcRelAssociatesMaterial am Exemplar, sonst am Typ), " +
        "uint16 L + L Byte UTF-8 Typname (ObjectType, sonst Name des Typobjekts), uint16 L + L Byte UTF-8 Fundstelle des Materials, " +
        "6 float32 Begrenzung, int32 Anzahl Punkte p, int32 Anzahl Dreiecke d, p*3 float32 Punkte x y z (Projekt-mm), " +
        "d*3 int32 Dreiecke (Punktindizes 0-basiert je Datensatz). Fassung 1 hat Rolle, Bezug und Fuellungen nicht, Fassungen 1 und 2 " +
        "haben Materialnamen, Typname und Fundstelle nicht. " +
        "Zerlegung (IfcRelAggregates, IfcRelNests unter einem IfcElement): ein Ganzes mit eigenem Koerper steht mit seinem Netz darin " +
        "und seine Teile zusaetzlich (Rolle teil, Bezug = Ganzes) - nicht doppelt pruefen; ein Ganzes ohne eigenen Koerper traegt das " +
        "Netz aller Teile, die Teile fehlen einzeln (ausser selbst uebernommene Teile eines nicht uebernommenen Ganzen). " +
        "Oeffnungen (IfcOpeningElement) sind eigene Datensaetze mit Kennzeichen 0, Bezug = Wirt ueber IfcRelVoidsElement (leer, wenn " +
        "die IFC-Datei keinen nennt); die Bauteilnetze sind nicht ausgeschnitten - Oeffnungen logisch abziehen, nicht zeichnen.";

    /// <summary>Repräsentationen, aus denen das Netz einer Öffnung kommt (wie IfcArchitektur bei Tür und Fenster).</summary>
    static readonly string[] OeffnungKennungen = ["Body", "Reference"];

    public (int Anzahl, double[]? Umriss) Erzeugen(IfcDatei datei, IReadOnlyCollection<IIfcProduct> produkte, IReadOnlySet<int> uebernommen, string ziel)
    {
        var g = IfcGeometrie.Von(datei);
        var labels = produkte.Select(p => p.EntityLabel).ToHashSet();

        // ---- Zerlegung: Ganzes je Teil (nur Teile in der Liste), Teile je Ganzem
        var ganzesVon = new Dictionary<int, IIfcElement>();
        var teileVon = new Dictionary<int, List<IIfcProduct>>();
        void Zerlegung(IIfcObjectDefinition? ganz, IEnumerable<IIfcObjectDefinition> teile)
        {
            if (ganz is not IIfcElement ge || ge is IIfcFeatureElementSubtraction) return;
            foreach (var t in teile.OfType<IIfcProduct>())
            {
                if (!labels.Contains(t.EntityLabel) || t.EntityLabel == ge.EntityLabel || !ganzesVon.TryAdd(t.EntityLabel, ge)) continue;
                if (!teileVon.TryGetValue(ge.EntityLabel, out var l)) teileVon[ge.EntityLabel] = l = [];
                l.Add(t);
            }
        }
        foreach (var r in datei.Modell.Instances.OfType<IIfcRelAggregates>()) Zerlegung(r.RelatingObject, r.RelatedObjects);
        foreach (var r in datei.Modell.Instances.OfType<IIfcRelNests>()) Zerlegung(r.RelatingObject, r.RelatedObjects);
        var material = new IfcNetzMaterial(datei);

        // ---- Ganze ohne eigenen Körper: tragen das Netz ihrer Teile (eigene Netze der Ganzen mit Körper hier schon geholt)
        var eigen = new Dictionary<int, Dreiecksnetz?>();          // eigenes Netz der Ganzen in der Liste (null = keins)
        var ohneKoerper = new Dictionary<int, IIfcElement>();      // Ganzes, das die Netze seiner Teile trägt
        var verbraucht = new Dictionary<int, IIfcElement>();       // Teil → Ganzes, in dessen Netz es aufgeht
        foreach (var (gl, teile) in teileVon)
        {
            var ge = ganzesVon[teile[0].EntityLabel];
            bool traegt;
            if (labels.Contains(gl))
            {
                var n = g.Herausnehmen(gl) ?? g.Koerper(ge, merken: false);
                eigen[gl] = n is { Leer: false } ? n : null;
                traegt = eigen[gl] is null;
            }
            else traegt = !IfcGeometrie.HatKoerper(ge);   // nicht in der Liste: ohne Repräsentation bzw. ohne Körper
            if (!traegt) continue;
            bool ganzesUebernommen = uebernommen.Contains(gl);
            foreach (var t in teile)
                if (ganzesUebernommen || !uebernommen.Contains(t.EntityLabel)) verbraucht[t.EntityLabel] = ge;
            if (teile.Any(t => verbraucht.ContainsKey(t.EntityLabel))) ohneKoerper[gl] = ge;
        }

        int anzahl = 0, mitNetz = 0;
        double[] b = [double.MaxValue, double.MaxValue, double.MaxValue, double.MinValue, double.MinValue, double.MinValue];
        var geschrieben = new Dictionary<int, string>();   // EntityLabel → GlobalId des Datensatzes, der seine Geometrie trägt
        using var fs = new FileStream(ziel, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1 << 20);
        using var w = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);
        w.Write(Encoding.ASCII.GetBytes(Kennung));
        w.Write(Fassung);
        var text = Encoding.UTF8.GetBytes(Beschreibung);
        w.Write(text.Length);
        w.Write(text);
        long kopf = fs.Position;
        w.Write(0);
        for (int i = 0; i < 6; i++) w.Write(0.0);

        // Netz aller verbrauchten Teile eines Ganzen (verschachtelt: ein Teil ohne Körper bringt die Netze seiner Teile mit)
        Dreiecksnetz? TeileNetz(IIfcElement ganz, int tiefe = 0)
        {
            var n = new Dreiecksnetz();
            foreach (var t in teileVon[ganz.EntityLabel])
            {
                if (!verbraucht.ContainsKey(t.EntityLabel)) continue;
                var tn = eigen.TryGetValue(t.EntityLabel, out var en) ? en : g.Herausnehmen(t.EntityLabel) ?? g.Koerper(t, merken: false);
                if ((tn is null || tn.Leer) && tiefe < 8 && ohneKoerper.TryGetValue(t.EntityLabel, out var tg)) tn = TeileNetz(tg, tiefe + 1);
                if (tn is { Leer: false }) n.Anhaengen(tn);
            }
            return n.Leer ? null : n;
        }

        // Material eines Ganzen ohne eigenen Körper: sein eigenes, sonst das der verbrauchten Teile (verschachtelt)
        IfcNetzMaterial.Angabe TeileMaterial(IIfcElement ganz, int tiefe = 0)
        {
            var eigenes = material.Von(ganz);
            if (eigenes.Namen.Count > 0 || tiefe >= 8) return eigenes;
            var teile = new List<IfcNetzMaterial.Angabe>();
            foreach (var t in teileVon[ganz.EntityLabel])
            {
                if (!verbraucht.ContainsKey(t.EntityLabel)) continue;
                teile.Add(ohneKoerper.TryGetValue(t.EntityLabel, out var tg) ? TeileMaterial(tg, tiefe + 1) : material.Von(t));
            }
            return IfcNetzMaterial.Vereinen(eigenes.Typname, teile);
        }

        void Produkt(IIfcProduct p, Dreiecksnetz n, IfcNetzMaterial.Angabe mat)
        {
            var (rolle, bezug) = ganzesVon.TryGetValue(p.EntityLabel, out var ge) ? (IfcNetzRolle.Teil, Gid(ge)) : (IfcNetzRolle.Bauteil, null);
            Schreiben(w, p, datei.GeschossVon(p)?.KoteMm, uebernommen.Contains(p.EntityLabel), rolle, bezug, [], mat, n, b);
            geschrieben[p.EntityLabel] = Gid(p);
            anzahl++; mitNetz++;
        }

        foreach (var p in produkte)
        {
            if (verbraucht.ContainsKey(p.EntityLabel)) continue;
            if (ohneKoerper.TryGetValue(p.EntityLabel, out var ganz))
            {
                if (TeileNetz(ganz) is { } tn) Produkt(p, tn, TeileMaterial(ganz));
                continue;
            }
            var netz = eigen.TryGetValue(p.EntityLabel, out var en) ? en : g.Herausnehmen(p.EntityLabel) ?? g.Koerper(p, merken: false);
            if (netz is null || netz.Leer) continue;
            Produkt(p, netz, material.Von(p));
        }
        // Ganze ohne eigenen Körper, die selbst nicht in der Liste stehen (keine Repräsentation): mit dem Netz ihrer Teile
        foreach (var (gl, ganz) in ohneKoerper.OrderBy(x => x.Key))
            if (!labels.Contains(gl) && TeileNetz(ganz) is { } tn) Produkt(ganz, tn, TeileMaterial(ganz));
        // ein verbrauchtes Teil ist dort «geschrieben», wo seine Geometrie steht: beim (obersten) Ganzen
        foreach (var t in verbraucht.Keys)
        {
            var x = t;
            for (int i = 0; i < 8 && verbraucht.TryGetValue(x, out var ganz); i++) x = ganz.EntityLabel;
            if (geschrieben.TryGetValue(x, out var gid)) geschrieben.TryAdd(t, gid);
        }

        // ---- Öffnungen: mit Wirt (IfcRelVoidsElement, Wirt in der Datei); ohne Wirt (Beziehung fehlt in der Datei, z. B. Revit-Export
        //      einer verknüpften Architektur) mit leerem Bezug, wenn sie im Bereich der geschriebenen Bauteile liegen
        var gesehen = new HashSet<int>();
        List<string> Fuellungen(IIfcOpeningElement o) =>
            o.HasFillings.Select(f => f.RelatedBuildingElement).Where(f => f is not null).Select(f => Gid(f!)).Distinct().ToList();
        foreach (var r in datei.Modell.Instances.OfType<IIfcRelVoidsElement>())
        {
            if (r.RelatedOpeningElement is not IIfcOpeningElement o || r.RelatingBuildingElement is not { } wirt || !gesehen.Add(o.EntityLabel)) continue;
            if (!geschrieben.TryGetValue(wirt.EntityLabel, out var wirtGid)) continue;
            if (g.Netz(o, OeffnungKennungen) is not { Leer: false } on) continue;
            Schreiben(w, o, (datei.GeschossVon(o) ?? datei.GeschossVon(wirt))?.KoteMm, false, IfcNetzRolle.Oeffnung, wirtGid, Fuellungen(o), IfcNetzMaterial.Angabe.Keine, on, null);
            anzahl++;
        }
        if (mitNetz > 0)
            foreach (var o in datei.Modell.Instances.OfType<IIfcOpeningElement>().OrderBy(x => x.EntityLabel))
            {
                if (gesehen.Contains(o.EntityLabel) || o.VoidsElements is not null) continue;
                if (g.Netz(o, OeffnungKennungen) is not { Leer: false } on || on.Begrenzung() is not { } ob) continue;
                if (ob[0] > b[3] || ob[3] < b[0] || ob[1] > b[4] || ob[4] < b[1] || ob[2] > b[5] || ob[5] < b[2]) continue;
                Schreiben(w, o, datei.GeschossVon(o)?.KoteMm, false, IfcNetzRolle.Oeffnung, null, Fuellungen(o), IfcNetzMaterial.Angabe.Keine, on, null);
                anzahl++;
            }
        g.Leeren();

        fs.Position = kopf;
        w.Write(anzahl);
        if (mitNetz == 0) b = [0, 0, 0, 0, 0, 0];
        foreach (var x in b) w.Write(x);
        w.Flush();
        return (anzahl, mitNetz == 0 ? null : [b[0], b[1], Math.Max(b[3], b[0] + 1), Math.Max(b[4], b[1] + 1)]);
    }

    static string Gid(IIfcRoot r) => (r.GlobalId.ToString() ?? "").PadRight(GidLaenge)[..GidLaenge];

    static void GidSchreiben(BinaryWriter w, string? gid) => w.Write(Encoding.ASCII.GetBytes((gid ?? "").PadRight(GidLaenge)[..GidLaenge]));

    /// <summary>Text als uint16 Länge + UTF-8 (höchstens <see cref="TextMax"/> Zeichen; null bzw. leer = Länge 0).</summary>
    internal static void Text16(BinaryWriter w, string? text)
    {
        var t = string.IsNullOrEmpty(text) ? "" : text.Length > TextMax ? text[..TextMax] : text;
        var bytes = Encoding.UTF8.GetBytes(t);
        w.Write((ushort)Math.Min(bytes.Length, ushort.MaxValue));
        w.Write(bytes, 0, Math.Min(bytes.Length, ushort.MaxValue));
    }

    static void Schreiben(BinaryWriter w, IIfcProduct p, double? kote, bool uebernommen, IfcNetzRolle rolle, string? bezug,
        IReadOnlyList<string> fuellungen, IfcNetzMaterial.Angabe material, Dreiecksnetz n, double[]? gesamt)
    {
        w.Write(p.EntityLabel);
        GidSchreiben(w, Gid(p));
        var ent = Encoding.ASCII.GetBytes(IfcDatei.Entitaet(p));
        w.Write((byte)Math.Min(ent.Length, 255));
        w.Write(ent, 0, Math.Min(ent.Length, 255));
        w.Write(kote ?? double.NaN);
        w.Write((byte)(uebernommen ? 1 : 0));
        w.Write((byte)rolle);
        GidSchreiben(w, bezug);
        int f = Math.Min(fuellungen.Count, 255);
        w.Write((byte)f);
        for (int i = 0; i < f; i++) GidSchreiben(w, fuellungen[i]);
        // Fassung 3: Material und Typname
        int m = Math.Min(material.Namen.Count, 255);
        w.Write((byte)m);
        for (int i = 0; i < m; i++) Text16(w, material.Namen[i]);
        Text16(w, material.Typname);
        Text16(w, material.Quelle);
        var bb = n.Begrenzung()!;
        foreach (var x in bb) w.Write((float)x);
        if (gesamt is not null)
            for (int i = 0; i < 3; i++) { gesamt[i] = Math.Min(gesamt[i], bb[i]); gesamt[i + 3] = Math.Max(gesamt[i + 3], bb[i + 3]); }
        w.Write(n.Punkte);
        w.Write(n.Dreiecke);
        foreach (var x in n.P) w.Write((float)x);
        foreach (var i in n.D) w.Write(i);
    }
}

/// <summary>Material und Typname eines Produkts für die Netzdatei Fassung 3 (Regeln im Dateikopf). Liest nur Namen – keine
/// Kennwerte.</summary>
public sealed class IfcNetzMaterial
{
    /// <summary>Materialnamen (in der Folge der Datei, ohne Doppel), Typname und Fundstelle.</summary>
    public sealed record Angabe(IReadOnlyList<string> Namen, string? Typname, string? Quelle)
    {
        public static readonly Angabe Keine = new([], null, null);
    }

    readonly IfcDatei datei;
    readonly Dictionary<int, IIfcMaterialSelect> zuordnung = [];

    public IfcNetzMaterial(IfcDatei datei)
    {
        this.datei = datei;
        foreach (var r in datei.Modell.Instances.OfType<IIfcRelAssociatesMaterial>())
            if (r.RelatingMaterial is { } m)
                foreach (var o in r.RelatedObjects) zuordnung.TryAdd(o.EntityLabel, m);
    }

    /// <summary>Material am Exemplar, sonst am Typobjekt; Typname = ObjectType, sonst Name des Typobjekts.</summary>
    public Angabe Von(IIfcProduct p)
    {
        var typ = p is IIfcObject ob ? datei.Typ(ob) : null;
        string? typname = IfcLauf.Text(p is IIfcObject o2 ? o2.ObjectType : null) ?? IfcLauf.Text(typ?.Name);
        var m = zuordnung.GetValueOrDefault(p.EntityLabel);
        string ort = "";
        if (m is null && typ is not null && zuordnung.TryGetValue(typ.EntityLabel, out var mt)) { m = mt; ort = $" am Typ #{typ.EntityLabel}"; }
        if (m is null) return new Angabe([], typname, null);
        var namen = Namen(m).Where(x => x is not null).Select(x => x!).Distinct(StringComparer.Ordinal).ToList();
        return new Angabe(namen, typname, $"{IfcDatei.Entitaet((Xbim.Common.IPersistEntity)m)} #{((Xbim.Common.IPersistEntity)m).EntityLabel}{ort}");
    }

    /// <summary>Material eines Ganzen aus dem seiner Teile (Namen vereint, Fundstellen gekürzt).</summary>
    public static Angabe Vereinen(string? typname, IReadOnlyList<Angabe> teile)
    {
        var namen = teile.SelectMany(t => t.Namen).Distinct(StringComparer.Ordinal).ToList();
        var quellen = teile.Select(t => t.Quelle).OfType<string>().Distinct().ToList();
        string? quelle = quellen.Count == 0 ? null : "Teile: " + string.Join("; ", quellen.Take(5)) + (quellen.Count > 5 ? $" … ({quellen.Count})" : "");
        return new Angabe(namen, typname ?? teile.Select(t => t.Typname).FirstOrDefault(t => t is not null), quelle);
    }

    /// <summary>Name eines IfcMaterial; IFC4 mit Category in Klammern, wenn sie nicht schon im Namen steht.</summary>
    static string? Name(IIfcMaterial? m)
    {
        if (m is null) return null;
        var n = IfcLauf.Text(m.Name);
        var k = m is Xbim.Ifc4.MaterialResource.IfcMaterial m4 ? IfcLauf.Text(m4.Category) : null;
        if (k is null || (n is not null && n.Contains(k, StringComparison.OrdinalIgnoreCase))) return n;
        return n is null ? k : $"{n} ({k})";
    }

    /// <summary>Namen aller Materialien der Zuordnung (am Xbim-Schema geprüft, siehe Dateikopf).</summary>
    static IEnumerable<string?> Namen(IIfcMaterialSelect m) => m switch
    {
        IIfcMaterial mat => [Name(mat)],
        IIfcMaterialList l => l.Materials.Select(Name),
        IIfcMaterialLayerSetUsage u => u.ForLayerSet?.MaterialLayers.Select(x => Name(x.Material)) ?? [],
        IIfcMaterialLayerSet s => s.MaterialLayers.Select(x => Name(x.Material)),
        IIfcMaterialLayer x => [Name(x.Material)],
        IIfcMaterialProfileSetUsage pu => pu.ForProfileSet?.MaterialProfiles.Select(x => Name(x.Material)) ?? [],
        IIfcMaterialProfileSet ps => ps.MaterialProfiles.Select(x => Name(x.Material)),
        IIfcMaterialProfile pr => [Name(pr.Material)],
        IIfcMaterialConstituentSet cs => cs.MaterialConstituents.Select(x => Name(x.Material)),
        IIfcMaterialConstituent c => [Name(c.Material)],
        _ => [],
    };
}

/// <summary>Liest eine Netzdatei (Fassung 1, 2 und 3): Produkte (Bauteile und Teile, nach GlobalId) und ihre Dreiecke – für den
/// Hintergrund und die Kollisionsprüfung (Modul 3b) –, dazu die Öffnungen (Fassung 2) getrennt davon.</summary>
public sealed class IfcNetzdatei
{
    public string Pfad { get; }
    public int Fassung { get; }
    public string Beschreibung { get; }
    /// <summary>[minx, miny, minz, maxx, maxy, maxz] in Projekt-mm.</summary>
    public double[] Begrenzung { get; }
    /// <summary>Bauteile und Teile (Rolle bauteil bzw. teil) – wie in Fassung 1 alles, was gezeichnet und geprüft wird.</summary>
    public IReadOnlyList<IfcNetzProdukt> Produkte { get; }
    /// <summary>Öffnungen (Rolle oeffnung, nur Fassung 2) – nicht zeichnen, logisch vom Wirt abziehen.</summary>
    public IReadOnlyList<IfcNetzProdukt> Oeffnungen { get; }
    readonly Dictionary<string, IfcNetzProdukt> nachGid;
    readonly Dictionary<string, List<IfcNetzProdukt>> teileVon;
    readonly Dictionary<string, List<IfcNetzProdukt>> oeffnungenVon;

    IfcNetzdatei(string pfad, int fassung, string beschreibung, double[] begrenzung, List<IfcNetzProdukt> produkte, List<IfcNetzProdukt> oeffnungen)
    {
        Pfad = pfad; Fassung = fassung; Beschreibung = beschreibung; Begrenzung = begrenzung; Produkte = produkte; Oeffnungen = oeffnungen;
        nachGid = new(StringComparer.Ordinal);
        teileVon = new(StringComparer.Ordinal);
        oeffnungenVon = new(StringComparer.Ordinal);
        foreach (var p in produkte)
        {
            nachGid.TryAdd(p.GlobalId, p);
            if (p.Rolle == IfcNetzRolle.Teil && p.Bezug is { } g) Liste(teileVon, g).Add(p);
        }
        foreach (var o in oeffnungen)
            if (o.Bezug is { } g) Liste(oeffnungenVon, g).Add(o);
    }

    static List<IfcNetzProdukt> Liste(Dictionary<string, List<IfcNetzProdukt>> d, string k)
    {
        if (!d.TryGetValue(k, out var l)) d[k] = l = [];
        return l;
    }

    /// <summary>Öffnet eine Netzdatei und liest die Kopfdaten aller Datensätze (ohne die Dreiecke).</summary>
    public static IfcNetzdatei Oeffnen(string pfad)
    {
        using var fs = new FileStream(pfad, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        using var r = new BinaryReader(fs, Encoding.UTF8);
        var kennung = Encoding.ASCII.GetString(r.ReadBytes(8));
        if (kennung != IfcNetzErzeuger.Kennung) throw new HlksFehler($"{Path.GetFileName(pfad)} ist keine Netzdatei.", "IFC erneut übernehmen (ifc_uebernehmen erzeugt die Netzdatei neu).");
        int fassung = r.ReadInt32();
        if (!IfcNetzErzeuger.LesbareFassungen.Contains(fassung))
            throw new HlksFehler($"Netzdatei {Path.GetFileName(pfad)} hat Fassung {fassung}, lesbar sind {string.Join(" und ", IfcNetzErzeuger.LesbareFassungen)}.", "IFC erneut übernehmen.");
        var text = Encoding.UTF8.GetString(r.ReadBytes(r.ReadInt32()));
        int anzahl = r.ReadInt32();
        var begrenzung = new double[6];
        for (int i = 0; i < 6; i++) begrenzung[i] = r.ReadDouble();
        var produkte = new List<IfcNetzProdukt>(anzahl);
        var oeffnungen = new List<IfcNetzProdukt>();
        for (int k = 0; k < anzahl; k++)
        {
            int label = r.ReadInt32();
            var gid = Gid(r);
            var ent = Encoding.ASCII.GetString(r.ReadBytes(r.ReadByte()));
            double kote = r.ReadDouble();
            bool ueb = r.ReadByte() == 1;
            var rolle = IfcNetzRolle.Bauteil;
            string? bezug = null;
            List<string> fuellungen = [];
            if (fassung >= 2)
            {
                byte ro = r.ReadByte();
                if (ro > (byte)IfcNetzRolle.Oeffnung) throw new HlksFehler($"Netzdatei {Path.GetFileName(pfad)}: unbekannte Rolle {ro} bei {gid}.", "IFC erneut übernehmen.");
                rolle = (IfcNetzRolle)ro;
                bezug = Gid(r) is { Length: > 0 } bz ? bz : null;
                int f = r.ReadByte();
                for (int i = 0; i < f; i++) fuellungen.Add(Gid(r));
            }
            List<string> materialien = [];
            string? typname = null, materialquelle = null;
            if (fassung >= IfcNetzErzeuger.FassungMaterial)
            {
                int nm = r.ReadByte();
                for (int i = 0; i < nm; i++) if (Text16(r) is { } t) materialien.Add(t);
                typname = Text16(r);
                materialquelle = Text16(r);
            }
            var bb = new float[6];
            for (int i = 0; i < 6; i++) bb[i] = r.ReadSingle();
            int punkte = r.ReadInt32(), dreiecke = r.ReadInt32();
            long lage = fs.Position;
            var pr = new IfcNetzProdukt(label, gid, ent, double.IsNaN(kote) ? null : kote, ueb, bb, punkte, dreiecke, lage)
            {
                Rolle = rolle, Bezug = bezug, Fuellungen = fuellungen, Materialien = materialien, Typname = typname, Materialquelle = materialquelle,
            };
            (rolle == IfcNetzRolle.Oeffnung ? oeffnungen : produkte).Add(pr);
            fs.Position = lage + punkte * 12L + dreiecke * 12L;
        }
        return new IfcNetzdatei(pfad, fassung, text, begrenzung, produkte, oeffnungen);
    }

    static string Gid(BinaryReader r) => Encoding.ASCII.GetString(r.ReadBytes(IfcNetzErzeuger.GidLaenge)).Trim();

    /// <summary>uint16 Länge + UTF-8 (Fassung 3); leer = null.</summary>
    static string? Text16(BinaryReader r)
    {
        int n = r.ReadUInt16();
        if (n == 0) return null;
        var t = Encoding.UTF8.GetString(r.ReadBytes(n)).Trim();
        return t.Length == 0 ? null : t;
    }

    /// <summary>Bauteil oder Teil mit dieser GlobalId (Öffnungen: <see cref="Oeffnung"/>).</summary>
    public IfcNetzProdukt? Produkt(string globalId) => nachGid.GetValueOrDefault(globalId);

    /// <summary>Öffnung mit dieser GlobalId.</summary>
    public IfcNetzProdukt? Oeffnung(string globalId) => Oeffnungen.FirstOrDefault(o => o.GlobalId == globalId);

    /// <summary>Teile (Rolle teil), deren Ganzes diese GlobalId hat (Fassung 2; sonst leer).</summary>
    public IReadOnlyList<IfcNetzProdukt> TeileVon(string ganzesGlobalId) => teileVon.TryGetValue(ganzesGlobalId, out var l) ? l : [];

    /// <summary>Öffnungen, deren Wirt diese GlobalId hat (Fassung 2; sonst leer).</summary>
    public IReadOnlyList<IfcNetzProdukt> OeffnungenVon(string wirtGlobalId) => oeffnungenVon.TryGetValue(wirtGlobalId, out var l) ? l : [];

    /// <summary>Punkte (x, y, z hintereinander, Projekt-mm) und Dreiecke (Indizes) eines Datensatzes.</summary>
    public (float[] Punkte, int[] Dreiecke) Netz(IfcNetzProdukt p)
    {
        using var fs = new FileStream(Pfad, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        return Lesen(fs, p);
    }

    static (float[], int[]) Lesen(FileStream fs, IfcNetzProdukt p)
    {
        fs.Position = p.Lage;
        var pb = new byte[p.Punkte * 12]; fs.ReadExactly(pb);
        var db = new byte[p.Dreiecke * 12]; fs.ReadExactly(db);
        var punkte = new float[p.Punkte * 3];
        var dreiecke = new int[p.Dreiecke * 3];
        Buffer.BlockCopy(pb, 0, punkte, 0, pb.Length);
        Buffer.BlockCopy(db, 0, dreiecke, 0, db.Length);
        return (punkte, dreiecke);
    }

    /// <summary>Alle Produkte (Bauteile und Teile, ohne Öffnungen) mit ihren Netzen (nurHintergrund = ohne die übernommenen).</summary>
    public IEnumerable<(IfcNetzProdukt Produkt, float[] Punkte, int[] Dreiecke)> Alle(bool nurHintergrund) =>
        Lesen(Produkte.Where(p => !(nurHintergrund && p.Uebernommen)));

    /// <summary>Alle Öffnungen mit ihren Netzen (Fassung 2; sonst leer).</summary>
    public IEnumerable<(IfcNetzProdukt Oeffnung, float[] Punkte, int[] Dreiecke)> AlleOeffnungen() => Lesen(Oeffnungen);

    IEnumerable<(IfcNetzProdukt, float[], int[])> Lesen(IEnumerable<IfcNetzProdukt> auswahl)
    {
        using var fs = new FileStream(Pfad, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20);
        foreach (var p in auswahl)
        {
            var (pu, dr) = Lesen(fs, p);
            yield return (p, pu, dr);
        }
    }
}
