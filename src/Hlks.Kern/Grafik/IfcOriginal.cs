// IFC 1:1 Schritt 2 (entwurf/ifc-1zu1.md; Christian 02.11.2026: «ein fenster muss 1:1 so aussehen wie im IFC. mit allen details» –
// «dies gilt für alle importierten familien. nicht nur für fenster.»): ein aus einer IFC-Unterlage übernommenes Element zeigt sein
// eigenes Netz aus der Netzdatei (mit den Farben der Teilkörper, Fassung 4) statt des Nachbaus – solange es nicht in KICAD geändert
// wurde (IfcUebernehmen.UnveraendertUebernommen, dieselbe Regel wie für die Kollision, entwurf/modul-1b.md: «Wird ein übernommenes
// Element in KICAD geändert, gilt für dieses Element nur noch die KICAD-Geometrie»). Teile eines Ganzen mit eigenem Körper (Rolle
// teil) gehören dazu. Die Logik (Wand, Öffnung, Raumgrenzen) rechnet weiter mit dem Nachbau; das hier ist nur die Darstellung.

using System.Collections.Concurrent;

namespace Hlks.Kern;

public static class IfcOriginal
{
    /// <summary>GlobalId, aus der das Element übernommen wurde (kennungen.ifc), sonst null.</summary>
    public static string? GlobalId(Element e) => e.Kennungen?.GetValueOrDefault("ifc") is { Length: > 0 } g ? g : null;

    static string Basis(string kennung) => kennung.IndexOf(':') is var i and > 0 ? kennung[..i] : kennung;

    /// <summary>Stücke eines auf mehrere Elemente verteilten IFC-Produkts (Kennung «GlobalId», «GlobalId:2» …, wie Pruefkoerperbau), je Modellstand.</summary>
    sealed class Stuecke(long zaehler, Dictionary<(string, string), List<Element>> gruppen)
    {
        public long Zaehler { get; } = zaehler;
        public Dictionary<(string, string), List<Element>> Gruppen { get; } = gruppen;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Modell, Stuecke> stuecke = new();

    static List<Element>? Gruppe(Modell m, string quelle, string basis)
    {
        if (!stuecke.TryGetValue(m, out var s) || s.Zaehler != m.Aenderungszaehler)
        {
            s = new Stuecke(m.Aenderungszaehler, m.Elemente.Where(e => e.Kennungen is { } kn && kn.ContainsKey("ifc") && kn.ContainsKey("quelle"))
                .GroupBy(e => (e.Kennungen!["quelle"], Basis(e.Kennungen["ifc"]))).Where(g => g.Count() > 1).ToDictionary(g => g.Key, g => g.ToList()));
            stuecke.AddOrUpdate(m, s);
        }
        return s.Gruppen.GetValueOrDefault((quelle, basis));
    }

    /// <summary>Unterlage und GlobalIds (Produkt und seine Teile) des IFC-Netzes, das dieses Element zeigt; leere Liste = ein weiteres Stück
    /// eines verteilten Produkts, das das erste Stück mit zeigt; null = Nachbau (geändert, Netz fehlt). Ein Produkt auf mehreren Elementen:
    /// nur wenn alle Stücke unverändert sind, sonst zeigen alle ihren Nachbau.</summary>
    public static (Element Unterlage, List<string> Produkte)? Netz(Projekt p, Element e)
    {
        if (GlobalId(e) is not { } gid || !IfcUebernehmen.UnveraendertUebernommen(e) || IfcDarstellung.UnterlageVon(p.Modell, e) is not { } u) return null;
        var basis = Basis(gid);
        var gruppe = Gruppe(p.Modell, e.Kennungen!["quelle"], basis);
        if (gruppe is not null && (!gruppe.All(IfcUebernehmen.UnveraendertUebernommen) || !gruppe.Any(x => x.Kennungen!["ifc"] == basis))) return null;
        if (Datei(p, u) is not { } d || d.Produkt(basis) is not { } pr) return null;
        if (gid != basis) return (u, []);   // vom ersten Stück mit dem Netz des ganzen Produkts gezeigt
        var produkte = new List<string>();
        if (pr.Dreiecke > 0) produkte.Add(basis);
        produkte.AddRange(d.TeileVon(basis).Where(t => t.Dreiecke > 0).Select(t => t.GlobalId));
        return produkte.Count > 0 ? (u, produkte) : null;
    }

    /// <summary>3D-Körper aus dem IFC-Netz (Ebene und Kennung wie der Nachbau); null, wenn das Element den Nachbau zeigt; ein Körper ohne
    /// Teile, wenn ein anderes Stück desselben Produkts es mit zeigt.</summary>
    public static Koerper? Koerper(Projekt p, Element e, Koerper? nachbau)
    {
        if (Netz(p, e) is not { } n) return null;
        if (n.Produkte.Count == 0) return new Koerper { Id = e.Id, Ebene = nachbau?.Ebene ?? "architektur" };
        return new Koerper
        {
            Id = e.Id, Ebene = nachbau?.Ebene ?? "architektur",
            Teile = [new Koerperteil { Art = "netz", Quelle = n.Unterlage.Id, Produkte = n.Produkte, Material = nachbau?.Teile.FirstOrDefault()?.Material ?? "sonst", IfcFarben = true, Deckkraft = 1 }],
        };
    }

    // ------------------------------------------------------------------ Netzdateien (Kopfdaten je Datei und Stand)

    static readonly ConcurrentDictionary<string, (DateTime Stand, IfcNetzdatei Datei)> dateien = new(StringComparer.OrdinalIgnoreCase);

    static IfcNetzdatei? Datei(Projekt p, Element unterlage)
    {
        if (unterlage.Text("netz") is not { Length: > 0 } name) return null;
        try
        {
            var pfad = Unterlagen.Pfad(p, name);
            if (!File.Exists(pfad)) return null;
            var stand = File.GetLastWriteTimeUtc(pfad);
            if (dateien.TryGetValue(pfad, out var x) && x.Stand == stand) return x.Datei;
            var d = IfcNetzdatei.Oeffnen(pfad);
            if (dateien.Count > 8) dateien.Clear();
            dateien[pfad] = (stand, d);
            return d;
        }
        catch (Exception ex) when (ex is HlksFehler or IOException) { return null; }
    }
}
