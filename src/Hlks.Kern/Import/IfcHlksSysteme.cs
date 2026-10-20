// IFC-Übertrag HLKS (Modul 1b, B2): Systeme, Anlagen und Stränge.
// IfcSystem/IfcDistributionSystem → System. Der Systemtyp ist Pflicht und kommt nur aus der Abbildung (ifc.json:
// Namensmuster über Name und ObjectType, sonst eindeutiger PredefinedType); ohne Systemtyp wird kein System angelegt,
// seine Mitglieder bleiben ohne System (Hinweis). Revit zerlegt ein System in viele Teile («H VL NT 6», «H VL NT 29» …):
// Teile mit gleichem Basisnamen (ohne Nummer am Ende) und gleichem Systemtyp werden ein System; ihre Kennung ist dann
// «system:<Basisname>», damit eine neue Fassung abgleicht, auch wenn Revit Teile neu nummeriert. Anlage je Gewerk
// (aus systemtypen.json), Kälte (Medien KAE-…) als eigene Anlage A-KAE im Gewerk Heizung, wenn das Projekt noch keine
// hat. Strang nur aus IfcDistributionCircuit (IFC4).

using System.Text.RegularExpressions;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

/// <summary>Ein KICAD-System aus einem oder mehreren IFC-Systemen (Revit-Teile mit gleichem Basisnamen).</summary>
public sealed class IfcHlksSystem
{
    public required string Basis { get; init; }
    public string? Systemtyp { get; init; }
    public string? Medium { get; init; }
    public string? Gewerk { get; init; }
    /// <summary>Wie der Systemtyp gefunden wurde (für Herkunft und Hinweis).</summary>
    public string? Grund { get; init; }
    public bool Zerlegt { get; init; }
    public List<IIfcGroup> Teile { get; } = [];
    /// <summary>Vorläufige Id des geplanten Systems (null, solange nicht geplant oder ohne Systemtyp).</summary>
    public string? Ref { get; set; }
}

public sealed class IfcHlksSysteme
{
    readonly IfcLauf lauf;
    readonly Dictionary<int, List<IfcHlksSystem>> jeObjekt = [];
    readonly Dictionary<int, IfcHlksSystem> jeTeil = [];
    readonly Dictionary<int, IIfcGroup> kreisVon = [];
    readonly Dictionary<int, string?> kreisRef = [];
    readonly Dictionary<string, string> anlagen = [];

    public List<IfcHlksSystem> Systeme { get; } = [];
    /// <summary>Anzahl IFC-Systeme (IfcSystem, IfcDistributionSystem ohne Stränge) in der Datei.</summary>
    public int IfcSysteme { get; private set; }

    IfcHlksSysteme(IfcLauf lauf) => this.lauf = lauf;

    /// <summary>IfcSystem (genau diese Entität) oder IfcDistributionSystem – nicht IfcZone, IfcBuildingSystem, Stränge.</summary>
    public static bool IstSystem(IPersistEntity g) => IfcDatei.Entitaet(g) == "IfcSystem" || (IfcHlksHilfe.IstArt(g, "IfcDistributionSystem") && !IstKreis(g));
    public static bool IstKreis(IPersistEntity g) => IfcHlksHilfe.IstArt(g, "IfcDistributionCircuit");

    /// <summary>Basisname: Name ohne Nummer am Ende («H VL NT 6» → «H VL NT»); bleibt nichts mit Buchstaben, der Name.</summary>
    public static string Basisname(string name)
    {
        var m = Regex.Match(name.Trim(), @"^(.*?\p{L}.*?)[\s_\-]*\d+$");
        return m.Success ? m.Groups[1].Value.Trim() : name.Trim();
    }

    public static IfcHlksSysteme Lesen(IfcLauf lauf)
    {
        var s = new IfcHlksSysteme(lauf);
        var d = lauf.Datei;
        var abb = lauf.Abbildung;
        var gruppen = d.Modell.Instances.OfType<IIfcRelAssignsToGroup>().ToList();
        var nachSchluessel = new Dictionary<string, IfcHlksSystem>();
        var basisTypen = new Dictionary<string, HashSet<string>>();
        foreach (var g in d.Modell.Instances.OfType<IIfcGroup>().Where(IstSystem).OrderBy(g => g.EntityLabel))
        {
            s.IfcSysteme++;
            var name = IfcLauf.Text(g.Name) ?? $"System #{g.EntityLabel}";
            var basis = Basisname(name);
            var ot = IfcLauf.Text(g.ObjectType);
            var pt = (g as IIfcDistributionSystem)?.PredefinedType?.ToString();
            var typ = abb.SystemtypFuer(pt is "NOTDEFINED" or "USERDEFINED" ? null : pt, basis, ot);
            var def = typ is not null && lauf.Katalog.Systemtypen.TryGetValue(typ, out var st) ? st : null;
            var schluessel = $"{basis}|{typ}";
            if (!nachSchluessel.TryGetValue(schluessel, out var sys))
            {
                sys = new IfcHlksSystem
                {
                    Basis = basis, Systemtyp = typ, Medium = def?.Medium, Gewerk = def?.Gewerk, Zerlegt = basis != name,
                    Grund = typ is null ? null : $"«{name}»{(ot is null ? "" : $", ObjectType «{ot}»")}{(pt is null or "NOTDEFINED" or "USERDEFINED" ? "" : $", PredefinedType {pt}")}",
                };
                nachSchluessel[schluessel] = sys;
                s.Systeme.Add(sys);
            }
            sys.Teile.Add(g);
            s.jeTeil[g.EntityLabel] = sys;
            if (typ is not null) (basisTypen.TryGetValue(basis, out var bt) ? bt : basisTypen[basis] = []).Add(typ);
        }
        s.mehrdeutigeBasis = basisTypen.Where(x => x.Value.Count > 1).Select(x => x.Key).ToHashSet();

        foreach (var r in gruppen)
        {
            if (s.jeTeil.TryGetValue(r.RelatingGroup.EntityLabel, out var sys))
                foreach (var o in r.RelatedObjects)
                {
                    if (!s.jeObjekt.TryGetValue(o.EntityLabel, out var l)) s.jeObjekt[o.EntityLabel] = l = [];
                    if (!l.Contains(sys)) l.Add(sys);
                }
            else if (IstKreis(r.RelatingGroup))
                foreach (var o in r.RelatedObjects) s.kreisVon.TryAdd(o.EntityLabel, r.RelatingGroup);
        }
        return s;
    }

    HashSet<string> mehrdeutigeBasis = [];

    /// <summary>Systeme, denen ein Produkt oder Port angehört (IfcRelAssignsToGroup).</summary>
    public IReadOnlyList<IfcHlksSystem> Von(IPersistEntity o) => jeObjekt.TryGetValue(o.EntityLabel, out var l) ? l : [];

    /// <summary>Strang (IfcDistributionCircuit), dem ein Produkt angehört, oder null.</summary>
    public IIfcGroup? KreisVon(IPersistEntity o) => kreisVon.GetValueOrDefault(o.EntityLabel);

    /// <summary>System, zu dem ein Strang gehört: Gruppe, der er zugewiesen ist, oder Aggregat.</summary>
    public IfcHlksSystem? SystemVonKreis(IIfcGroup kreis)
    {
        foreach (var sys in Von(kreis)) if (sys.Systemtyp is not null) return sys;
        return lauf.Datei.Eltern(kreis) is { } e && jeTeil.TryGetValue(e.EntityLabel, out var s) && s.Systemtyp is not null ? s : null;
    }

    /// <summary>Plant das System (einmal) und gibt seine vorläufige Id zurück; null ohne Systemtyp.</summary>
    public string? Planen(IfcHlksSystem sys)
    {
        if (sys.Systemtyp is null) return null;
        if (sys.Ref is not null) return sys.Ref;
        var erster = sys.Teile[0];
        var e = lauf.Neu("System", erster);
        foreach (var t in sys.Teile.Skip(1)) lauf.Behandelt.Add(t.EntityLabel);
        if (sys.Zerlegt || sys.Teile.Count > 1)
        {
            // Kennung aus dem Basisnamen: Revit nummeriert die Teile je Export neu
            var kennung = "system:" + sys.Basis + (mehrdeutigeBasis.Contains(sys.Basis) ? "|" + sys.Systemtyp : "");
            lauf.Geplant.Remove(e.Id);
            e.Id = "ifc:" + kennung;
            e.Kennungen!["ifc"] = kennung;
            e.Name = sys.Basis;
            e.Angelegt = e.Angelegt! with { Quelle = $"{lauf.Quelle}, {sys.Teile.Count} Teile von «{sys.Basis}» ({string.Join(", ", sys.Teile.Take(5).Select(t => "#" + t.EntityLabel))}{(sys.Teile.Count > 5 ? " …" : "")})" };
            lauf.Geplant[e.Id] = e;
        }
        e.Kennzeichen = Kurz(sys.Basis);
        lauf.Wert(e, "systemtyp", sys.Systemtyp, $"{IfcDatei.Entitaet(erster)} {sys.Grund} über die Namensmuster in ifc.json");
        if (sys.Medium is not null)
            e.Eigenschaften["medium"] = new Wert { Inhalt = sys.Medium, Herkunft = lauf.Herkunft with { Art = "katalog", Quelle = $"Systemtyp «{sys.Systemtyp}»" } };
        if (sys.Gewerk is not null) e.TeilVon = AnlageRef(AnlageGruppe(sys.Gewerk, sys.Medium));
        sys.Ref = e.Id;
        return sys.Ref;
    }

    /// <summary>Plant den Strang (IfcDistributionCircuit) eines Produkts; null, wenn es keinem angehört oder der Strang
    /// keinem System mit Systemtyp zugeordnet ist.</summary>
    public (string Strang, string System)? StrangPlanen(IPersistEntity mitglied)
    {
        if (KreisVon(mitglied) is not { } kreis) return null;
        var sys = SystemVonKreis(kreis);
        if (sys is null || Planen(sys) is not { } sysRef) { kreisRef.TryAdd(kreis.EntityLabel, null); return null; }
        if (!kreisRef.TryGetValue(kreis.EntityLabel, out var r) || r is null)
        {
            var e = lauf.Neu("Strang", kreis);
            e.TeilVon = sysRef;
            e.System = sysRef;
            kreisRef[kreis.EntityLabel] = r = e.Id;
        }
        return (r, sysRef);
    }

    /// <summary>Stränge ohne System (für den Hinweis).</summary>
    public int StraengeOhneSystem => kreisRef.Count(x => x.Value is null);

    /// <summary>Gruppe der Anlage: das Gewerk, ausser Kälte (Medium KAE-…): eigene Anlage «kaelte» im Gewerk Heizung
    /// (Entscheid Christian 20.10.2026: «Eigene Anlage unter Heizung» – Kältekreise bleiben getrennt gruppiert).</summary>
    public static string AnlageGruppe(string gewerk, string? medium) =>
        gewerk == "heizung" && medium is not null && medium.StartsWith("KAE-", StringComparison.OrdinalIgnoreCase) ? "kaelte" : gewerk;

    /// <summary>Anlage je Gruppe (Gewerk bzw. Kälte): eine bestehende (gleiche Kennung oder alle ihre Systeme in dieser
    /// Gruppe) oder neu.</summary>
    string AnlageRef(string gruppe)
    {
        if (anlagen.TryGetValue(gruppe, out var r)) return r;
        var kennung = "anlage:" + gruppe;
        var arbeit = lauf.Kontext.Arbeit;
        var da = arbeit.DerKlasse("Anlage").OrderBy(a => a.Kennzeichen).FirstOrDefault(a => a.Kennungen?.GetValueOrDefault("ifc") == kennung)
                 ?? arbeit.DerKlasse("Anlage").OrderBy(a => a.Kennzeichen).FirstOrDefault(a =>
                     arbeit.Kinder(a.Id).Where(x => x.Klasse == "System").ToList() is { Count: > 0 } sys
                     && sys.All(x => x.Text("systemtyp") is { } t && lauf.Katalog.Systemtypen.TryGetValue(t, out var st) && AnlageGruppe(st.Gewerk, st.Medium) == gruppe));
        if (da is not null && da.Kennungen?.GetValueOrDefault("quelle") != lauf.UnterlageId) return anlagen[gruppe] = da.Id;
        var (name, kz) = gruppe switch
        {
            "sanitaer" => ("Sanitäranlage", "A-SAN"),
            "heizung" => ("Heizungsanlage", "A-HZG"),
            "kaelte" => ("Kälteanlage", "A-KAE"),
            "lueftung" => ("Lüftungsanlage", "A-LUE"),
            _ => ($"Anlage {gruppe}", "A"),
        };
        var gewerk = gruppe == "kaelte" ? "heizung" : gruppe;
        var e = new Element
        {
            Id = "ifc:" + kennung, Klasse = "Anlage", Name = name, Kennzeichen = da?.Kennzeichen ?? kz,
            Kennungen = new() { ["ifc"] = kennung, ["quelle"] = lauf.UnterlageId },
            Angelegt = lauf.Herkunft with { Quelle = $"{lauf.Quelle}: je Gewerk aus den Systemtypen" },
        };
        e.Eigenschaften["beschreibung"] = new Wert
        {
            Inhalt = gruppe == "kaelte" ? $"Kältesysteme (Gewerk heizung) aus {lauf.Quelle}" : $"Systeme des Gewerks {gewerk} aus {lauf.Quelle}",
            Herkunft = lauf.Herkunft with { Quelle = $"{lauf.Quelle}: je Gewerk aus den Systemtypen" },
        };
        lauf.Geplant[e.Id] = e;
        return anlagen[gruppe] = e.Id;
    }

    /// <summary>Kennzeichen aus dem Basisnamen: Leerzeichen → «-», höchstens 30 Zeichen.</summary>
    static string Kurz(string basis)
    {
        var s = Regex.Replace(basis.Trim(), @"\s+", "-");
        return s.Length > 30 ? s[..30] : s;
    }
}
