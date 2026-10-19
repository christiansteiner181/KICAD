// Ein IFC-Übertrag (Modul 1b) in Arbeit: was die Übertrager planen, bevor IfcUebernehmen abgleicht und übernimmt.
// Die Übertrager (je Bereich eine Datei) erzeugen Elemente mit vorläufigen Ids «ifc:<GlobalId>» und verweisen damit
// auch untereinander (Tür → bezuege.wand, Leitung → system, Element → raum). IfcUebernehmen ersetzt sie durch echte
// Ids: bestehende Elemente mit derselben GlobalId behalten ihre Id (Abgleich), neue bekommen eine.

using System.Text.Json.Nodes;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Übertrager eines Bereichs: plant KICAD-Elemente aus IFC-Objekten.</summary>
public interface IIfcUebertrag
{
    /// <summary>architektur | raeume | hlks</summary>
    string Bereich { get; }
    /// <summary>Klassen, die dieser Übertrager anlegt. Ein erneuter Übertrag löscht nur Elemente dieser Klassen, die aus
    /// derselben Unterlage stammen und in der neuen Fassung fehlen.</summary>
    IReadOnlySet<string> Klassen { get; }
    /// <summary>Plant die Elemente seines Bereichs (lauf.Neu …). Was er nicht versteht, bleibt unbehandelt und kommt in
    /// den Hintergrund.</summary>
    void Uebertragen(IfcLauf lauf);
}

/// <summary>Netzdatei einer IFC-Unterlage: exakte Geometrie aller Produkte (Christian 17.10.2026: Grundlage für
/// Kollisionsprüfung und Aussparungen). Die nicht übernommenen bilden den Hintergrund (3D und Grundrissschnitt).</summary>
public interface IIfcGrundlage
{
    /// <summary>Schreibt die Netzdatei nach ziel: alle produkte, die in uebernommen (EntityLabels) gekennzeichnet.
    /// Rückgabe: Anzahl Produkte im Netz und Umriss [minx, miny, maxx, maxy] in Projekt-mm (null, wenn leer).</summary>
    (int Anzahl, double[]? Umriss) Erzeugen(IfcDatei datei, IReadOnlyCollection<IIfcProduct> produkte, IReadOnlySet<int> uebernommen, string ziel);
}

public sealed record IfcVerbindung(string VonRef, string VonAnschluss, string NachRef, string NachAnschluss);

public sealed class IfcLauf
{
    public required IfcDatei Datei { get; init; }
    public required Kontext Kontext { get; init; }
    public Projekt Projekt => Kontext.Projekt;
    public Katalog Katalog => Kontext.Katalog;
    public IfcAbbildung Abbildung => Katalog.Ifc ?? throw new HlksFehler("Die IFC-Abbildung (kataloge/ifc.json) fehlt.");
    /// <summary>Id der Unterlage, aus der übernommen wird (Kennung «quelle» der Elemente).</summary>
    public required string UnterlageId { get; init; }
    /// <summary>Text für die Herkunft, z. B. «arch.ifc (IFC2X3, Archicad 27.0.0)».</summary>
    public required string Quelle { get; init; }
    /// <summary>Herkunft der übernommenen Werte (Art «ifc»).</summary>
    public required Herkunft Herkunft { get; init; }
    /// <summary>IFC-Geschoss (zusammengefasst) → Id des Geschoss-Elements.</summary>
    public required IReadOnlyDictionary<IfcGeschoss, string> GeschossIds { get; init; }
    /// <summary>Nur diese Geschoss-Ids übernehmen; null = alle.</summary>
    public IReadOnlySet<string>? GeschossFilter { get; init; }

    /// <summary>Geplante Elemente, Schlüssel = vorläufige Id «ifc:&lt;GlobalId&gt;».</summary>
    public Dictionary<string, Element> Geplant { get; } = [];
    public List<IfcVerbindung> Verbindungen { get; } = [];
    /// <summary>EntityLabels der übernommenen IFC-Objekte (der Rest kommt in den Hintergrund).</summary>
    public HashSet<int> Behandelt { get; } = [];
    public List<string> Hinweise { get; } = [];
    /// <summary>IFC-Entität → Anzahl, die kein Übertrager verstanden hat (gehen in den Hintergrund).</summary>
    public Dictionary<string, int> NichtVerstanden { get; } = [];
    /// <summary>Geplantes Element (vorläufige Id) übernimmt ein bestehendes Element (dessen Id): Id und alle Verweise darauf
    /// bleiben, Inhalt und Kennungen kommen aus dem IFC. Zweck: ein Raum aus IfcSpace ersetzt einen früher aus den Wänden
    /// gebildeten Raum am selben Ort (Befund Abnahme 1b, 18.10.2026). Gilt nur, wenn die GlobalId noch nicht im Bestand ist.</summary>
    public Dictionary<string, string> Uebernimmt { get; } = [];
    /// <summary>Bestehende Elemente, die ein geplantes Element ersetzt (bestehende Id → vorläufige Id): Verweise gehen auf das
    /// geplante, das bestehende wird gelöscht.</summary>
    public Dictionary<string, string> Ersetzt { get; } = [];

    /// <summary>Vorläufige Id eines IFC-Objekts (für Verweise zwischen geplanten Elementen).</summary>
    public static string Ref(IIfcRoot r) => "ifc:" + r.GlobalId;

    /// <summary>Id des Geschoss-Elements, in dem das Objekt steht, oder null.</summary>
    public string? GeschossId(IIfcObjectDefinition o) => Datei.GeschossVon(o) is { } g && GeschossIds.TryGetValue(g, out var id) ? id : null;

    /// <summary>Liegt das Produkt in einem gewählten Geschoss? (ohne Filter immer; ohne Geschoss nur ohne Filter)</summary>
    public bool ImFilter(IIfcObjectDefinition o) => GeschossFilter is null || (GeschossId(o) is { } g && GeschossFilter.Contains(g));

    /// <summary>Plant ein neues Element aus einem IFC-Objekt (oder holt das schon geplante). Name aus IFC, Geschoss aus der
    /// räumlichen Gliederung, Kennungen ifc + quelle, Herkunft «ifc» mit Entität und Fundstelle. Markiert das Objekt als
    /// übernommen.</summary>
    public Element Neu(string klasse, IIfcRoot quelle, string? geschossId = null)
    {
        var id = Ref(quelle);
        if (Geplant.TryGetValue(id, out var da)) return da;
        var label = quelle is IPersistEntity pe ? pe.EntityLabel : 0;
        var e = new Element
        {
            Id = id,
            Klasse = klasse,
            Name = Text(quelle.Name),
            Geschoss = geschossId ?? (quelle is IIfcObjectDefinition od ? GeschossId(od) : null),
            Kennungen = new() { ["ifc"] = quelle.GlobalId.ToString(), ["quelle"] = UnterlageId },
            Angelegt = Herkunft with { Quelle = $"{Quelle}, {IfcDatei.Entitaet((IPersistEntity)quelle)} #{label}" },
        };
        Geplant[id] = e;
        if (label != 0) Behandelt.Add(label);
        return e;
    }

    /// <summary>Setzt eine Eigenschaft mit Herkunft «ifc» und Fundstelle (z. B. «Pset_WallCommon.IsExternal»). Die Einheit
    /// kommt aus dem Katalog.</summary>
    public void Wert(Element e, string eigenschaft, JsonNode? wert, string? fundstelle = null)
    {
        if (wert is null) return;
        var def = Katalog.Klassen.TryGetValue(e.Klasse, out var d) ? d.Eigenschaften.FirstOrDefault(x => x.Name.Equals(eigenschaft, StringComparison.OrdinalIgnoreCase)) : null;
        if (def is null) throw new HlksFehler($"Übertrag: Eigenschaft «{eigenschaft}» gibt es für {e.Klasse} nicht.");
        e.Eigenschaften[def.Name] = new Wert
        {
            Inhalt = wert,
            Einheit = def.Einheit,
            Herkunft = Herkunft with { Quelle = fundstelle is null ? Quelle : $"{Quelle}, {fundstelle}" },
        };
    }

    /// <summary>Verbindung zwischen zwei Anschlüssen geplanter Elemente (über ihre IFC-Objekte).</summary>
    public void Verbinden(IIfcRoot von, string vonAnschluss, IIfcRoot nach, string nachAnschluss) =>
        Verbindungen.Add(new IfcVerbindung(Ref(von), vonAnschluss, Ref(nach), nachAnschluss));

    /// <summary>Zählt eine nicht verstandene Entität (für die Übersicht).</summary>
    public void NichtVerstandenZaehlen(IPersistEntity e)
    {
        var n = IfcDatei.Entitaet(e);
        NichtVerstanden[n] = NichtVerstanden.GetValueOrDefault(n) + 1;
    }

    public static string? Text(object? ifcText)
    {
        var s = ifcText?.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }
}
