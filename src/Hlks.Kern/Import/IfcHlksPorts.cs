// IFC-Übertrag HLKS (Modul 1b, B2): Ports (IfcDistributionPort) und ihre Verbindungen. Ports hängen in IFC4 über
// IfcRelNests am Element, in IFC2x3 über IfcRelConnectsPortToElement – Trimble Nova benutzt auch in IFC4 die alte
// Beziehung, beides wird gelesen. Verbunden sind sie über IfcRelConnectsPorts. Die Fliessrichtung steht an den Ports
// (FlowDirection); fehlt sie, sagt es manchmal der Port gegenüber (Revit: Rohre SOURCEANDSINK, Formteile In/Out).

using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Ein Port eines Produkts mit Lage (Projekt-mm), Achse (Einheitsvektor, Vorzeichen je Programm verschieden)
/// und Fliessrichtung.</summary>
public sealed class IfcHlksPort
{
    public required IIfcDistributionPort Port { get; init; }
    public required IIfcProduct Element { get; init; }
    public string Name => IfcLauf.Text(Port.Name) ?? $"#{Port.EntityLabel}";
    public required double[] Lage { get; init; }
    public double[]? Achse { get; init; }
    /// <summary>rein | raus aus FlowDirection (SINK | SOURCE); null = nicht angegeben, SOURCEANDSINK oder NOTDEFINED.</summary>
    public string? Fluss { get; set; }
    /// <summary>port = aus FlowDirection des Ports; gegenueber = umgekehrte Richtung des verbundenen Ports.</summary>
    public string? FlussQuelle { get; set; }
    public IfcHlksPort? Gegenueber { get; set; }
}

/// <summary>Alle Ports einer Datei: je Produkt sortiert, mit Gegenüber und Fliessrichtung.</summary>
public sealed class IfcHlksPorts
{
    readonly Dictionary<int, List<IfcHlksPort>> jeElement = [];
    readonly Dictionary<int, IfcHlksPort> jePort = [];
    public int Verbindungen { get; private set; }
    public int MehrfachVerbunden { get; private set; }

    public IReadOnlyList<IfcHlksPort> Von(IIfcProduct p) => jeElement.TryGetValue(p.EntityLabel, out var l) ? l : [];
    public IfcHlksPort? Port(IPersistEntity port) => jePort.GetValueOrDefault(port.EntityLabel);
    public IEnumerable<IfcHlksPort> Alle => jePort.Values;

    public static IfcHlksPorts Lesen(IfcDatei d, IfcAbbildung abb)
    {
        var aus = new IfcHlksPorts();
        var traeger = new Dictionary<int, (IIfcDistributionPort Port, IIfcProduct Element)>();
        foreach (var r in d.Modell.Instances.OfType<IIfcRelConnectsPortToElement>())
            if (r.RelatingPort is IIfcDistributionPort dp && r.RelatedElement is IIfcProduct el) traeger.TryAdd(dp.EntityLabel, (dp, el));
        foreach (var r in d.Modell.Instances.OfType<IIfcRelNests>())
            if (r.RelatingObject is IIfcProduct el)
                foreach (var dp in r.RelatedObjects.OfType<IIfcDistributionPort>()) traeger.TryAdd(dp.EntityLabel, (dp, el));

        foreach (var (_, (dp, el)) in traeger)
        {
            var w = d.ZuModell * d.Welt(dp.ObjectPlacement);
            var fluss = abb.FlussAus(dp.FlowDirection?.ToString());
            var p = new IfcHlksPort
            {
                Port = dp, Element = el, Lage = Runden(w.Ursprung),
                Achse = dp.ObjectPlacement is null ? null : w.AchseZ,
                Fluss = fluss is "rein" or "raus" ? fluss : null,
                FlussQuelle = fluss is "rein" or "raus" ? "port" : null,
            };
            aus.jePort[dp.EntityLabel] = p;
            if (!aus.jeElement.TryGetValue(el.EntityLabel, out var l)) aus.jeElement[el.EntityLabel] = l = [];
            l.Add(p);
        }
        foreach (var l in aus.jeElement.Values)
            l.Sort((a, b) => Reihenfolge(a).CompareTo(Reihenfolge(b)) is var c && c != 0 ? c : a.Port.EntityLabel.CompareTo(b.Port.EntityLabel));

        foreach (var r in d.Modell.Instances.OfType<IIfcRelConnectsPorts>())
        {
            if (aus.Port(r.RelatingPort) is not { } a || aus.Port(r.RelatedPort) is not { } b) continue;
            if (a.Gegenueber is not null || b.Gegenueber is not null) { aus.MehrfachVerbunden++; continue; }
            a.Gegenueber = b; b.Gegenueber = a;
            aus.Verbindungen++;
        }
        // Fehlt die Richtung, aber der verbundene Port hat eine, gilt die Gegenrichtung (raus dort = rein hier)
        foreach (var p in aus.jePort.Values)
            if (p.Fluss is null && p.Gegenueber is { FlussQuelle: "port", Fluss: { } g })
            {
                p.Fluss = g == "raus" ? "rein" : "raus";
                p.FlussQuelle = "gegenueber";
            }
        return aus;
    }

    /// <summary>Reihenfolge der Ports: Nummer am Ende des Namens (CP1, CP2 … bei Nova; InPort_…_0, OutPort_…_1 bei
    /// Revit), sonst Dateireihenfolge.</summary>
    static long Reihenfolge(IfcHlksPort p)
    {
        var m = Regex.Match(p.Name, @"(\d+)\s*$");
        return m.Success && long.TryParse(m.Groups[1].Value, out var n) ? n : long.MaxValue;
    }

    static double[] Runden(double[] p) => [Math.Round(p[0], 1), Math.Round(p[1], 1), Math.Round(p[2], 1)];
}
