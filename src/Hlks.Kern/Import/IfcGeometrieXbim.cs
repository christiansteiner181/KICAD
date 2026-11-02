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
    static int dienste;   // 0 = noch nicht, 1 = eingerichtet, -1 = nicht verfügbar

    /// <summary>Teilkörper, die xBIM mit ungültigen Koordinaten lieferte und die verworfen wurden (seit Programmstart).</summary>
    public static int Verworfen { get; private set; }

    /// <summary>Warum xBIM nicht rechnet (Umgebung, Engine fehlt, Fehler) – sonst null.</summary>
    public static string? Grund { get; private set; }

    /// <summary>Soll xBIM rechnen? Nur unter Windows und nicht mit HLKS_XBIM_GEOMETRIE=0.</summary>
    public static bool Ein => OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("HLKS_XBIM_GEOMETRIE") != "0" && dienste >= 0;

    /// <summary>Richtet die Dienste von xBIM ein (vor dem ersten Öffnen eines Modells; Modellwahl wie bisher heuristisch).</summary>
    public static void DiensteEinrichten()
    {
        if (dienste != 0 || !OperatingSystem.IsWindows()) return;
        try { XbimServices.Current.ConfigureServices(s => s.AddXbimToolkit(c => c.AddHeuristicModel().AddGeometryServices())); dienste = 1; }
        catch (Exception ex) { dienste = -1; Grund = $"xBIM-Geometrie nicht eingerichtet: {ex.Message}"; }
    }

    /// <summary>Netze aller Produkte mit Körper in Projektkoordinaten (mm), mit den Farben der Teilkörper; null, wenn xBIM nicht rechnet.
    /// Roh = Körper der eigenen Darstellung samt ihren Booleans (Zuschnitte), ohne Öffnungen (IfcRelVoidsElement) – Massstab für den Nachbau,
    /// der die Öffnungen selbst führt; Anzeige = mit ausgeschnittenen Öffnungen, wie ein IFC-Betrachter es zeigt (Netzdatei, 3D).</summary>
    public static (Dictionary<int, Dreiecksnetz> Roh, Dictionary<int, Dreiecksnetz> Anzeige)? Netze(IfcDatei d, Func<IIfcRepresentationItem, uint> stilfarbe, Func<IIfcProduct, uint> materialfarbe)
    {
        DiensteEinrichten();
        if (!Ein) return null;
        try
        {
            var ctx = new Xbim3DModelContext(d.Modell, NullLoggerFactory.Instance, Xbim.Geometry.Abstractions.XGeometryEngineVersion.V6, "model", null);
            if (!ctx.CreateContext(null, false)) { Grund = "xBIM-Geometrie: Kontext nicht erstellt"; return null; }
            var zuModell = d.ZuModell;
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
                int basis = n.Punkte;
                foreach (var v in tri.Vertices)
                {
                    var p = zuModell.Punkt([v.X * mm, v.Y * mm, v.Z * mm]);
                    n.Punkt(p[0], p[1], p[2]);
                }
                foreach (var f in tri.Faces)
                    for (int i = 0; i + 2 < f.Indices.Count; i += 3)
                        n.Dreieck(basis + f.Indices[i], basis + f.Indices[i + 1], basis + f.Indices[i + 2]);
            }
            // ohne Öffnungen gibt es nur die eine Fassung: dann ist sie auch der rohe Körper
            foreach (var (label, n) in anzeige) roh.TryAdd(label, n);
            return (roh, anzeige);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Grund = $"xBIM-Geometrie fehlgeschlagen ({ex.GetType().Name}: {ex.Message}) – eigene Auswertung";
            return null;
        }
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
