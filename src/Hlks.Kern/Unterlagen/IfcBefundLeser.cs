// Leser «ifc» für Hersteller-IFC mit Typen (Modul 2, Auftrag B2): xBIM wie in Modul 1b (IfcDatei, IfcGeometrie).
//   typ      je Typobjekt (IfcValveType, …): Name, IFC-Klasse, PredefinedType, Anzahl Exemplare
//   pset     je Property Set eines Typs bzw. Produkts: Eigenschaften mit Werten (Längenmasse in mm umgerechnet)
//   port     je IfcDistributionPort: Lage und Richtung (Achse z des Ports), Fluss (rein, raus, beide), Systemtyp, Eigenschaften
//   form3d   je Produkt mit Körper: Netz (Ecken und Dreiecke) und Hüllquader – aus der Geometrie von Modul 1b
// Koordinaten: lokal zum Produkt (seine Platzierung ist der Ursprung), mm – so liegen Körper und Anschlüsse in Familien-
// koordinaten, wenn der Hersteller das Produkt im Ursprung platziert hat. Die Welt-Lage steht im Zusatz «welt».
// Die Richtung eines Ports (Achse z seiner Platzierung) zeigt je Programm nach innen oder aussen (Befund Modul 1b): sie wird
// als «richtung» gemeldet, mit Hinweis; die KI prüft sie an der Geometrie.
// Annahme: Ein Hersteller-IFC hat wenige Produkte. Bei mehr als 50 Produkten mit Körper werden nur die ersten 50 als form3d gemeldet.

using System.Reflection;
using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcBefundLeser : IUnterlagenLeser
{
    public string Name => "ifc";
    public int Version => 1;
    public IReadOnlyCollection<string> Arten { get; } = ["ifc", "ifc-typ"];

    public const int MaxProdukte = 50, MaxDreieckeJeProdukt = 150_000;

    public UnterlagenBefunde Lesen(string pfad, string datei, string pruefsumme, string art)
    {
        var s = new BefundSammler(this, datei, pruefsumme, art);
        using var d = IfcDatei.Oeffnen(pfad);
        s.Hinweis($"IFC {d.Schema}, Programm {(string.IsNullOrWhiteSpace(d.Programm) ? "unbekannt" : d.Programm)}, Längeneinheit {d.MmJeEinheit:0.####} mm.");

        // Typobjekte
        var typen = d.Modell.Instances.OfType<IIfcTypeObject>().OrderBy(t => t.EntityLabel).ToList();
        var exemplare = new Dictionary<int, int>();
        foreach (var r in d.Modell.Instances.OfType<IIfcRelDefinesByType>())
            exemplare[r.RelatingType.EntityLabel] = exemplare.GetValueOrDefault(r.RelatingType.EntityLabel) + r.RelatedObjects.Count;
        foreach (var t in typen)
        {
            var wert = new JsonObject { ["name"] = IfcLauf.Text(t.Name), ["ifcKlasse"] = IfcDatei.Entitaet(t), ["globalId"] = t.GlobalId.ToString() };
            if (Vordefiniert(t) is { } pt && pt != "NOTDEFINED") wert["predefinedType"] = pt;
            if (t is IIfcElementType et && IfcLauf.Text(et.ElementType) is { } elt) wert["elementType"] = elt;
            if (IfcLauf.Text(t.Description) is { } beschr) wert["beschreibung"] = beschr;
            var tb = s.Neu("typ", wert, zusatz: new JsonObject { ["exemplare"] = exemplare.GetValueOrDefault(t.EntityLabel), ["ifcId"] = $"#{t.EntityLabel}" });
            foreach (var (pset, werte) in Psets(d, t.HasPropertySets))
                s.Neu("pset", PsetWert(pset, werte, d), zusatz: new JsonObject { ["typ"] = IfcLauf.Text(t.Name), ["typBefund"] = tb.Id, ["quelle"] = "typ" });
        }
        if (typen.Count == 0) s.Hinweis("Die Datei enthält keine Typobjekte (IfcTypeObject); Werte stehen nur an den Exemplaren.");

        // Produkte mit Körper oder Ports
        var produkte = d.Modell.Instances.OfType<IIfcProduct>()
            .Where(p => p is not (IIfcDistributionPort or IIfcSpatialStructureElement or IIfcOpeningElement or IIfcAnnotation or IIfcGrid))
            .Where(p => IfcGeometrie.HatKoerper(p) || (p is IIfcObject o && PortsVon(d, p).Count > 0))
            .OrderBy(p => p.EntityLabel).ToList();
        if (produkte.Count == 0) s.Hinweis("Die Datei enthält kein Produkt mit Körper oder Ports.");
        if (produkte.Count > MaxProdukte)
            s.Hinweis($"Die Datei hat {produkte.Count} Produkte mit Körper oder Ports (kein einzelnes Herstellermodell?): gemeldet werden die ersten {MaxProdukte}.");
        var geo = IfcGeometrie.Von(d);
        foreach (var p in produkte.Take(MaxProdukte))
        {
            var lage = d.Lage(p);
            var zurueck = lage.InverseStarr();
            var typ = p is IIfcObject obj ? d.Typ(obj) : null;
            var produktZusatz = new JsonObject
            {
                ["produkt"] = IfcLauf.Text(p.Name), ["ifcKlasse"] = IfcDatei.Entitaet(p), ["globalId"] = p.GlobalId.ToString(),
            };
            if (typ is not null) produktZusatz["typ"] = IfcLauf.Text(typ.Name);

            // form3d
            Dreiecksnetz? netz = null;
            try { netz = IfcGeometrie.HatKoerper(p) ? geo.Koerper(p, merken: false) : null; }
            catch (Exception ex) when (ex is not OutOfMemoryException) { s.Hinweis($"Körper von «{IfcLauf.Text(p.Name)}» nicht lesbar: {ex.Message}"); }
            if (netz is not null && !netz.Leer)
            {
                var lokal = netz.Abgebildet(Affin.Aus(zurueck));
                Form3d(s, lokal, produktZusatz, lage, d);
            }
            else if (IfcGeometrie.HatKoerper(p))
                s.Hinweis($"Der Körper von «{IfcLauf.Text(p.Name)}» ({IfcDatei.Entitaet(p)}) ergab kein Netz (nicht unterstützte Geometrie: {string.Join(", ", geo.Unbekannt.Select(x => $"{x.Key} {x.Value}"))}).");

            // Ports
            foreach (var port in PortsVon(d, p))
            {
                var w = zurueck * (d.ZuModell * d.Welt(port.ObjectPlacement));
                var z = (JsonObject)produktZusatz.DeepClone(); z["ifcId"] = $"#{port.EntityLabel}";
                var wert = new JsonObject
                {
                    ["name"] = IfcLauf.Text(port.Name),
                    ["lage"] = Bau.Feld(Rund(w.Ursprung)),
                };
                if (port.ObjectPlacement is not null) wert["richtung"] = Bau.Feld(Rund(w.AchseZ, 4));
                var fd = port.FlowDirection?.ToString();
                wert["flussDirection"] = fd;
                wert["fluss"] = fd switch { "SOURCE" => "raus", "SINK" => "rein", "SOURCEANDSINK" => "beide", _ => null };
                var sys = Roh(port, "SystemType"); if (sys is not null && sys != "NOTDEFINED") wert["systemtyp"] = sys;
                var vorb = Roh(port, "PredefinedType"); if (vorb is not null && vorb != "NOTDEFINED") wert["art"] = vorb;
                var eig = new JsonObject();
                foreach (var (schluessel, v) in d.Eigenschaften(port)) eig[schluessel] = Wert(v, d, out _);
                if (eig.Count > 0) wert["eigenschaften"] = eig;
                z["welt"] = Bau.Feld(Rund(d.ZuModell.Punkt(d.Welt(port.ObjectPlacement).Ursprung)));
                z["koordinaten"] = "lokal zum Produkt, mm";
                s.Neu("port", wert, "mm", lage: [Rund(w.Ursprung)], sicherheit: 1, zusatz: z);
            }

            // Psets des Exemplars
            if (p is IIfcObject o2)
            {
                foreach (var (pset, werte) in ExemplarPsets(d, o2))
                    s.Neu("pset", PsetWert(pset, werte, d), zusatz: new JsonObject { ["produkt"] = IfcLauf.Text(p.Name), ["typ"] = typ is null ? null : IfcLauf.Text(typ.Name), ["quelle"] = "exemplar" });
            }
        }
        if (produkte.Count > 0 && s.Ergebnis.Befunde.All(b => b.Art != "port"))
            s.Hinweis("Die Produkte haben keine Ports (IfcDistributionPort): Anschlusslagen müssen aus der Geometrie oder dem Datenblatt kommen.");
        if (s.Ergebnis.Befunde.Any(b => b.Art == "port"))
            s.Hinweis("Port-Richtung = Achse z der Port-Platzierung; je Programm zeigt sie in das Gerät hinein oder aus ihm heraus – an der Geometrie prüfen.");
        return s.Ergebnis;
    }

    // ---------- form3d ----------

    static void Form3d(BefundSammler s, Dreiecksnetz n, JsonObject produktZusatz, Matrix4 lage, IfcDatei d)
    {
        var b = n.Begrenzung()!;
        var min = new[] { b[0], b[1], b[2] }; var max = new[] { b[3], b[4], b[5] };
        var groesse = new[] { max[0] - min[0], max[1] - min[1], max[2] - min[2] };
        var wert = new JsonObject
        {
            ["huelle"] = new JsonObject { ["min"] = Bau.Feld(Rund(min)), ["max"] = Bau.Feld(Rund(max)), ["groesse"] = Bau.Feld(Rund(groesse)) },
            ["anzahlEcken"] = n.Punkte, ["anzahlDreiecke"] = n.Dreiecke,
        };
        var z = (JsonObject)produktZusatz.DeepClone();
        z["koordinaten"] = "lokal zum Produkt (Platzierung = Ursprung), mm";
        z["welt"] = Bau.Feld(Rund(d.ZuModell.Punkt(lage.Ursprung)));
        if (n.Dreiecke <= MaxDreieckeJeProdukt)
        {
            wert["ecken"] = new JsonArray(Enumerable.Range(0, n.Punkte).Select(i => (JsonNode)new JsonArray(Math.Round(n.P[3 * i], 3), Math.Round(n.P[3 * i + 1], 3), Math.Round(n.P[3 * i + 2], 3))).ToArray());
            wert["dreiecke"] = new JsonArray(Enumerable.Range(0, n.Dreiecke).Select(i => (JsonNode)new JsonArray(n.D[3 * i], n.D[3 * i + 1], n.D[3 * i + 2])).ToArray());
        }
        else s.Hinweis($"Körper mit {n.Dreiecke} Dreiecken (mehr als {MaxDreieckeJeProdukt}): nur Hüllquader und Anzahl im Befund.");
        s.Neu("form3d", wert, "mm", lage: [Rund(min), Rund(max)], box: [Math.Round(min[0], 3), Math.Round(min[1], 3), Math.Round(max[0], 3), Math.Round(max[1], 3)], sicherheit: 1, zusatz: z);
    }

    // ---------- Ports ----------

    static List<IIfcDistributionPort> PortsVon(IfcDatei d, IIfcProduct p)
    {
        var l = new List<IIfcDistributionPort>();
        foreach (var r in d.Modell.Instances.OfType<IIfcRelConnectsPortToElement>())
            if (r.RelatedElement.EntityLabel == p.EntityLabel && r.RelatingPort is IIfcDistributionPort dp) l.Add(dp);
        foreach (var r in d.Modell.Instances.OfType<IIfcRelNests>())
            if (r.RelatingObject.EntityLabel == p.EntityLabel) l.AddRange(r.RelatedObjects.OfType<IIfcDistributionPort>());
        return l.DistinctBy(x => x.EntityLabel).OrderBy(x => x.EntityLabel).ToList();
    }

    // ---------- Property Sets ----------

    static IEnumerable<(string Name, List<(string Name, IfcWert Wert)> Werte)> Psets(IfcDatei d, IEnumerable<IIfcPropertySetDefinition> defs)
    {
        foreach (var def in defs)
        {
            var name = IfcLauf.Text(def.Name) ?? "";
            var werte = new List<(string, IfcWert)>();
            if (def is IIfcPropertySet ps)
                foreach (var pr in ps.HasProperties)
                {
                    switch (pr)
                    {
                        case IIfcPropertySingleValue sv when sv.NominalValue is not null:
                            werte.Add((pr.Name.ToString()!, new IfcWert(sv.NominalValue.Value, sv.NominalValue.GetType().Name)));
                            break;
                        case IIfcPropertyEnumeratedValue ev:
                            werte.Add((pr.Name.ToString()!, new IfcWert(string.Join(", ", ev.EnumerationValues.Select(x => x?.Value?.ToString())), "IfcLabel")));
                            break;
                        case IIfcPropertyBoundedValue bv:
                            werte.Add((pr.Name.ToString()!, new IfcWert($"{bv.LowerBoundValue?.Value}…{bv.UpperBoundValue?.Value}", "IfcLabel")));
                            break;
                        case IIfcPropertyListValue lv:
                            werte.Add((pr.Name.ToString()!, new IfcWert(string.Join(", ", lv.ListValues.Select(x => x.Value?.ToString())), "IfcLabel")));
                            break;
                    }
                }
            else if (def is IIfcElementQuantity q)
                foreach (var m in q.Quantities)
                {
                    IfcWert? w = m switch
                    {
                        IIfcQuantityLength l => new IfcWert((double)l.LengthValue, "IfcLengthMeasure"),
                        IIfcQuantityArea a => new IfcWert((double)a.AreaValue, "IfcAreaMeasure"),
                        IIfcQuantityVolume v => new IfcWert((double)v.VolumeValue, "IfcVolumeMeasure"),
                        IIfcQuantityCount c => new IfcWert((double)c.CountValue, "IfcCountMeasure"),
                        IIfcQuantityWeight g => new IfcWert((double)g.WeightValue, "IfcMassMeasure"),
                        _ => null,
                    };
                    if (w is not null) werte.Add((m.Name.ToString()!, w));
                }
            if (werte.Count > 0) yield return (name, werte);
        }
    }

    static IEnumerable<(string Name, List<(string Name, IfcWert Wert)> Werte)> ExemplarPsets(IfcDatei d, IIfcObject o)
    {
        var defs = new List<IIfcPropertySetDefinition>();
        foreach (var r in d.Modell.Instances.OfType<IIfcRelDefinesByProperties>())
        {
            if (!r.RelatedObjects.Any(x => x.EntityLabel == o.EntityLabel)) continue;
            if (r.RelatingPropertyDefinition is IIfcPropertySetDefinition def) defs.Add(def);
        }
        return Psets(d, defs);
    }

    static JsonObject PsetWert(string name, List<(string Name, IfcWert Wert)> werte, IfcDatei d)
    {
        var eig = new JsonObject(); var einheiten = new JsonObject(); var arten = new JsonObject();
        foreach (var (n, w) in werte)
        {
            eig[n] = Wert(w, d, out var einheit);
            if (einheit is not null) einheiten[n] = einheit;
            arten[n] = w.Art;
        }
        var o = new JsonObject { ["pset"] = name, ["eigenschaften"] = eig };
        if (einheiten.Count > 0) o["einheiten"] = einheiten;
        o["messarten"] = arten;
        return o;
    }

    /// <summary>Wert als Zahl oder Text; Längenmasse in mm umgerechnet.</summary>
    static JsonNode? Wert(IfcWert w, IfcDatei d, out string? einheit)
    {
        einheit = null;
        switch (w.Inhalt)
        {
            case double x when w.Art.Contains("LengthMeasure", StringComparison.OrdinalIgnoreCase):
                einheit = "mm"; return Math.Round(x * d.MmJeEinheit, 4);
            case double x: return Math.Round(x, 6);
            case long l: return l;
            case int i: return i;
            case bool b: return b;
            case null: return null;
            default: return w.Inhalt.ToString();
        }
    }

    static string? Vordefiniert(object o) => Roh(o, "PredefinedType");

    static string? Roh(object o, string eigenschaft) => o.GetType().GetProperty(eigenschaft, BindingFlags.Public | BindingFlags.Instance)?.GetValue(o)?.ToString();

    static double[] Rund(double[] p, int stellen = 3) => p.Select(x => Math.Round(x, stellen) + 0.0).ToArray();
}
