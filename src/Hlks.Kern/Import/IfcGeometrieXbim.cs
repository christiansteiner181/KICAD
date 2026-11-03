// IFC-Geometrie über die xBIM-Geometrie-Engine (OpenCascade), Entscheid Christian 02.11.2026 («xBIM-Geometrie (OpenCascade)»; «der ifc import
// ist die basis für alles folgende, dieser muss immer 100% passen»). Die eigene Auswertung (IfcGeometrie.cs) nimmt bei IfcBooleanResult nur
// den ersten Operanden – Archicad schreibt Öffnungen und Zuschnitte als Differenzen (Bernstrasse 42: 20 200), dann stimmen weder das Netz
// noch die Masse des Nachbaus. xBIM rechnet die Körper samt Booleans exakt; IfcGeometrie.Koerper nimmt dieses Netz, sobald es vorliegt.
//
// Lizenzen: Xbim.Geometry CDDL-1.0, Xbim.Geometry.Occt (OpenCascade 7.8.1) LGPL-2.1 mit OCCT-Ausnahme – unverändert genutzt. Nativ, nur
// Windows (C++/CLI); anderswo, bei einem Fehler der Engine oder mit HLKS_XBIM_GEOMETRIE=0 rechnet KICAD wie bisher selbst (Hinweis
// «vereinfacht» bleibt dann). Ein Modell wird einmal ganz gerechnet (Bernstrasse 42: rund 40 s) und je Datei zwischengespeichert.

using Microsoft.Extensions.Logging.Abstractions;
using Xbim.Common.Configuration;
using Xbim.Common.Geometry;
using Xbim.Common.XbimExtensions;
using Xbim.Ifc4.Interfaces;
using Xbim.ModelGeometry.Scene;

namespace Hlks.Kern;

public static class IfcGeometrieXbim
{
    static int dienste;
    /// <summary>Ein Modell zugleich: die native Engine (OpenCascade) stürzte ab, wenn mehrere Läufe parallel rechneten (Tests, 03.11.2026).</summary>
    static readonly object sperre = new();   // 0 = noch nicht, 1 = eingerichtet, -1 = nicht verfügbar

    /// <summary>Teilkörper, die xBIM mit ungültigen Koordinaten lieferte und die verworfen wurden (seit Programmstart).</summary>
    public static int Verworfen { get; private set; }

    /// <summary>Warum xBIM nicht rechnet (Umgebung, Engine fehlt, Fehler) – sonst null.</summary>
    public static string? Grund { get; private set; }

    /// <summary>Soll xBIM rechnen? Nur unter Windows und nicht mit HLKS_XBIM_GEOMETRIE=0.</summary>
    public static bool Ein => OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("HLKS_XBIM_GEOMETRIE") != "0" && dienste >= 0 && !Aussetzen.Value;

    /// <summary>Im laufenden Ablauf ohne xBIM rechnen (Tests der eigenen Auswertung, die bei fehlendem xBIM gilt); gilt nur für diesen Ablauf.</summary>
    public static readonly AsyncLocal<bool> Aussetzen = new();

    /// <summary>Richtet die Dienste von xBIM ein (vor dem ersten Öffnen eines Modells; Modellwahl wie bisher heuristisch).</summary>
    public static void DiensteEinrichten()
    {
        if (dienste != 0 || !OperatingSystem.IsWindows()) return;
        try { XbimServices.Current.ConfigureServices(s => s.AddXbimToolkit(c => c.AddHeuristicModel().AddGeometryServices())); dienste = 1; }
        catch (Exception ex) { dienste = -1; Grund = $"xBIM-Geometrie nicht eingerichtet: {ex.Message}"; }
    }

    /// <summary>Netze aller Produkte mit Körper in Weltkoordinaten der Datei (mm, ohne Bezug – IfcGeometrie bildet sie auf den jeweiligen Bezug ab), mit den Farben der Teilkörper; null, wenn xBIM nicht rechnet.
    /// Roh = Körper der eigenen Darstellung samt ihren Booleans (Zuschnitte), ohne Öffnungen (IfcRelVoidsElement) – Massstab für den Nachbau,
    /// der die Öffnungen selbst führt; Anzeige = mit ausgeschnittenen Öffnungen, wie ein IFC-Betrachter es zeigt (Netzdatei, 3D).</summary>
    public static (Dictionary<int, Dreiecksnetz> Roh, Dictionary<int, Dreiecksnetz> Anzeige)? Netze(IfcDatei d, Func<IIfcRepresentationItem, uint> stilfarbe, Func<IIfcProduct, uint> materialfarbe)
    {
        DiensteEinrichten();
        if (!Ein) return null;
        lock (sperre)
        try
        {
            var ctx = new Xbim3DModelContext(d.Modell, NullLoggerFactory.Instance, Xbim.Geometry.Abstractions.XGeometryEngineVersion.V6, "model", null);
            if (!ctx.CreateContext(null, false)) { Grund = "xBIM-Geometrie: Kontext nicht erstellt"; return null; }
            double mm = d.MmJeEinheit;
            var roh = new Dictionary<int, Dreiecksnetz>();
            var anzeige = new Dictionary<int, Dreiecksnetz>();
            foreach (var s in ctx.ShapeInstances())
            {
                var aus = s.RepresentationType switch
                {
                    XbimGeometryRepresentationType.OpeningsAndAdditionsIncluded => anzeige,
                    XbimGeometryRepresentationType.OpeningsAndAdditionsExcluded => roh,
                    _ => null,
                };
                if (aus is null) continue;
                var g = ctx.ShapeGeometry(s);
                if (g.Format != XbimGeometryType.PolyhedronBinary) continue;
                using var br = new BinaryReader(new MemoryStream(((IXbimShapeGeometryData)g).ShapeData));
                var tri = br.ReadShapeTriangulation().Transform(s.Transformation);
                // entartete Teilkörper (Koordinaten NaN/unendlich) verwerfen – sie würden das Netz und jede Masse daraus verderben
                if (tri.Vertices.Count == 0 || tri.Vertices.Any(v => !double.IsFinite(v.X) || !double.IsFinite(v.Y) || !double.IsFinite(v.Z) || Math.Abs(v.X) > 1e9 || Math.Abs(v.Y) > 1e9 || Math.Abs(v.Z) > 1e9))
                { Verworfen++; continue; }
                if (!aus.TryGetValue(s.IfcProductLabel, out var n))
                {
                    aus[s.IfcProductLabel] = n = new Dreiecksnetz();
                    n.FarbeSetzen(d.Modell.Instances[s.IfcProductLabel] is IIfcProduct pr ? materialfarbe(pr) : 0);
                }
                uint grund = n.Farben.Count > 0 ? n.Farben[0].Farbe : 0;
                uint farbe = d.Modell.Instances[g.IfcShapeLabel] is IIfcRepresentationItem it ? stilfarbe(it) : 0;
                n.FarbeSetzen(farbe != 0 ? farbe : grund);
                Anhaengen(n, tri, v => [v.X * mm, v.Y * mm, v.Z * mm]);
            }
            // ohne Öffnungen gibt es nur die eine Fassung: dann ist sie auch der rohe Körper
            foreach (var (label, n) in anzeige) roh.TryAdd(label, n);
            Rueckfall(d, roh, anzeige, stilfarbe, materialfarbe);
            return (roh, anzeige);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Grund = $"xBIM-Geometrie fehlgeschlagen ({ex.GetType().Name}: {ex.Message}) – eigene Auswertung";
            return null;
        }
    }

    /// <summary>Produkte, deren Körper der Rückfall (Booleans einzeln) gerechnet hat – zuletzt gerechnetes Modell.</summary>
    public static int Nachgerechnet { get; private set; }

    /// <summary>Rückfall für Produkte mit Körper, die der Kontext ganz verwarf: OpenCascade meldet bei einer langen Kette von Differenzen
    /// «Boolean Result returned an empty shape», wenn sich Flächen nur berühren (Archicad-Teile der Träger, Bernstrasse 42: 98 Teile, 03.11.2026).
    /// Einzeln gerechnet – Differenz um Differenz mit der Modelltoleranz, eine, die den Körper leer machen würde, ausgelassen – stimmt der
    /// Körper. Nur direkte Elemente der Körperdarstellung (keine IfcMappedItem); sonst bleibt es bei der eigenen Auswertung.</summary>
    static void Rueckfall(IfcDatei d, Dictionary<int, Dreiecksnetz> roh, Dictionary<int, Dreiecksnetz> anzeige,
        Func<IIfcRepresentationItem, uint> stilfarbe, Func<IIfcProduct, uint> materialfarbe)
    {
        Nachgerechnet = 0;
        Xbim.Geometry.Engine.Interop.XbimGeometryEngine? eng = null;
        var log = NullLoggerFactory.Instance.CreateLogger("xbim");
        var mf = d.Modell.ModelFactors;
        double mm = d.MmJeEinheit;
        foreach (var p in d.Modell.Instances.OfType<IIfcProduct>())
        {
            if (roh.ContainsKey(p.EntityLabel) || p is IIfcFeatureElementSubtraction or IIfcSpatialElement) continue;
            if (IfcGeometrie.Elemente(p, IfcGeometrie.KoerperKennungen) is not { } items) continue;
            if (items.Any(i => i is not IIfcGeometricRepresentationItem || i is IIfcMappedItem)) continue;
            eng ??= new Xbim.Geometry.Engine.Interop.XbimGeometryEngine(d.Modell, NullLoggerFactory.Instance);
            var n = new Dreiecksnetz();
            n.FarbeSetzen(materialfarbe(p));
            uint grund = n.FarbeAktuell;
            var welt = d.Welt(p.ObjectPlacement);
            try
            {
                foreach (var it in items.Cast<IIfcGeometricRepresentationItem>())
                {
                    if (Koerper(eng, it, mf.Precision, log) is not { } koerper) continue;
                    var g = eng.CreateShapeGeometry(koerper, mf.Precision, mf.DeflectionTolerance, mf.DeflectionAngle, XbimGeometryType.PolyhedronBinary, log);
                    using var br = new BinaryReader(new MemoryStream(((IXbimShapeGeometryData)g).ShapeData));
                    var tri = br.ReadShapeTriangulation();
                    if (tri.Vertices.Count == 0) continue;
                    uint farbe = stilfarbe(it);
                    n.FarbeSetzen(farbe != 0 ? farbe : grund);
                    Anhaengen(n, tri, v => welt.Punkt([v.X * mm, v.Y * mm, v.Z * mm]));
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { continue; }
            if (n.Leer) continue;
            roh[p.EntityLabel] = n;
            anzeige.TryAdd(p.EntityLabel, n);
            Nachgerechnet++;
        }
    }

    /// <summary>Körper eines Darstellungselements; eine Kette von Differenzen (IfcBooleanResult, links verschachtelt) Schritt für Schritt.</summary>
    static IXbimGeometryObject? Koerper(Xbim.Geometry.Engine.Interop.XbimGeometryEngine eng, IIfcGeometricRepresentationItem it, double toleranz, Microsoft.Extensions.Logging.ILogger log)
    {
        if (it is not IIfcBooleanResult b) return eng.Create(it, log);
        var kette = new List<IIfcBooleanResult>();
        for (IIfcBooleanResult? x = b; x is not null; x = x.FirstOperand as IIfcBooleanResult) kette.Add(x);
        kette.Reverse();
        if (kette.Any(k => k.Operator != IfcBooleanOperator.DIFFERENCE) || kette[0].FirstOperand is not IIfcGeometricRepresentationItem basis) return null;
        var aktuell = AlsMenge(eng, eng.Create(basis, log));
        if (aktuell is null) return null;
        foreach (var k in kette)
        {
            if (k.SecondOperand is not IIfcGeometricRepresentationItem zweit || AlsMenge(eng, eng.Create(zweit, log)) is not { } weg) continue;
            try
            {
                var r = aktuell.Cut(weg, toleranz, log);
                if (r.Sum(q => q.Volume) > 1e-12) aktuell = r;   // leer: diese Differenz auslassen (berührende Flächen)
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { }
        }
        return aktuell;
    }

    static IXbimSolidSet? AlsMenge(Xbim.Geometry.Engine.Interop.XbimGeometryEngine eng, IXbimGeometryObject? g)
    {
        if (g is IXbimSolidSet m) return m;
        if (g is not IXbimSolid s) return null;
        var aus = eng.CreateSolidSet();
        aus.Add(s);
        return aus;
    }

    /// <summary>Dreiecke einer xBIM-Triangulierung anhängen, Punkte über <paramref name="punkt"/> in Welt-mm. Umlaufsinn nach aussen: bei
    /// gespiegelter Platzierung (Determinante < 0) dreht xBIM die Dreiecke nicht mit – die Trittflächen einer Treppe zeigten dann nach unten
    /// (Bernstrasse 42, IfcStair #177903, 03.11.2026). Massstab: Vorzeichen des eingeschlossenen Volumens.</summary>
    static void Anhaengen(Dreiecksnetz n, XbimShapeTriangulation tri, Func<XbimPoint3D, double[]> punkt)
    {
        var p = tri.Vertices.Select(punkt).ToList();
        var (x0, y0, z0) = (p[0][0], p[0][1], p[0][2]);
        double vol = 0;
        foreach (var f in tri.Faces)
            for (int i = 0; i + 2 < f.Indices.Count; i += 3)
            {
                var (a, b, c) = (p[f.Indices[i]], p[f.Indices[i + 1]], p[f.Indices[i + 2]]);
                double ax = a[0] - x0, ay = a[1] - y0, az = a[2] - z0, bx = b[0] - x0, by = b[1] - y0, bz = b[2] - z0, cx = c[0] - x0, cy = c[1] - y0, cz = c[2] - z0;
                vol += ax * (by * cz - bz * cy) - ay * (bx * cz - bz * cx) + az * (bx * cy - by * cx);
            }
        int basis = n.Punkte;
        foreach (var q in p) n.Punkt(q[0], q[1], q[2]);
        foreach (var f in tri.Faces)
            for (int i = 0; i + 2 < f.Indices.Count; i += 3)
                if (vol < 0) n.Dreieck(basis + f.Indices[i], basis + f.Indices[i + 2], basis + f.Indices[i + 1]);
                else n.Dreieck(basis + f.Indices[i], basis + f.Indices[i + 1], basis + f.Indices[i + 2]);
    }
}

/// <summary>Richtet die xBIM-Dienste beim Laden des Kerns ein – vor jedem anderen Gebrauch von xBIM (auch IfcStore.Create in Tests oder Exporten),
/// sonst baut xBIM seine Dienste ohne Geometrie und lässt sie nicht mehr ändern.</summary>
static class IfcGeometrieXbimStart
{
#pragma warning disable CA2255   // bewusst: Dienste vor jedem Gebrauch von xBIM
    [System.Runtime.CompilerServices.ModuleInitializer]
#pragma warning restore CA2255
    internal static void Einrichten() => IfcGeometrieXbim.DiensteEinrichten();
}
