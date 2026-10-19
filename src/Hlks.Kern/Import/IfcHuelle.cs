// Übertrag der Gebäudehülle aus IFC (Modul 1c, Auftrag B4, entwurf/modul-1c.md Abschnitt «IFC»): Materialschichten werden
// Aufbauten, IfcMaterial wird Material, die thermischen Kennwerte der Datei gehen mit Fundstelle in die Elemente, die
// Nordrichtung (TrueNorth) wird Vorschlag für Projektinformation.nordwinkel. Läuft im Bereich «architektur» nach
// IfcArchitektur (Reihenfolge der Übertrager: Bereich, dann Klassenname) und ergänzt dessen geplante Bauteile in lauf.Geplant.
//
// Grundsatz (Modul 1c): Keine Kennwerte aus dem Gedächtnis – übernommen wird nur, was in der Datei steht, mit Fundstelle.
//
// Regeln (Annahmen gekennzeichnet):
// - Aufbau: je Bauteil (Wand, Decke, Dach, Tür) mit IfcMaterialLayerSetUsage/IfcMaterialLayerSet (am Exemplar, sonst am Typ).
//   Kennung «aufbau:<Prüfsumme aus Art, Material und Dicke der Schichten innen → aussen>», gleiche Schichtfolge = derselbe
//   Aufbau. Name = Kurzbeschreibung wie Bauteil.aufbau, aber innen → aussen. Schichten ohne Dicke (0) entfallen, eine Schicht
//   ohne Material verhindert den Aufbau (Hinweis). Fenster haben keinen Aufbau (Aufbau.art kennt kein «fenster»).
// - Reihenfolge innen → aussen: Richtung der Schichtfolge (LayerSetDirection, DirectionSense, Lage des Bauteils) gegen die
//   Aussenseite. Wand: Aussenseite = die Seite, auf der kein umschlossener Raum liegt (die Wände des Geschosses ergeben
//   umschlossene Flächen; liegt auf genau einer Seite der Wand eine, ist das die Innenseite). Decke/Boden: Annahme Raumseite
//   oben (Schichten von oben nach unten), Dach: Raumseite unten. Fehlt die Richtung (nur IfcMaterialLayerSet) oder lässt sich die
//   Seite nicht bestimmen (Innenwand, offene Wände, Tür), gilt die Reihenfolge der Datei – gezählt im Hinweis.
// - Material: Kennung «material:<Name>»; es entsteht für jedes IfcMaterial, das ein übernommenes Bauteil verwendet (Schichten,
//   Material, Materialliste, Konstituenten, Profile). Gleicher Name = dasselbe Material. Kennwerte nur, wenn die Datei sie
//   hat: Pset_MaterialThermal.ThermalConductivity → lambda, .SpecificHeatCapacity → waermekapazitaet, Pset_MaterialCommon.
//   MassDensity → rohdichte (IFC4: IfcMaterialProperties; IFC2x3: IfcThermalMaterialProperties, IfcGeneralMaterialProperties
//   und IfcExtendedMaterialProperties). Einheit: aus den abgeleiteten Einheiten der Datei (ohne Angabe SI wie im IFC-Standard);
//   weicht sie von SI ab, wird umgerechnet und im Hinweis genannt, ist sie nicht umrechenbar, entfällt der Wert.
// - Gibt es dieselbe Kennung schon bei einer anderen Unterlage (zweite IFC-Datei mit gleichen Materialnamen), bekommt die
//   neue ein «@<Unterlage>» angehängt – sonst würden sich die Dateien die Elemente beim Abgleich gegenseitig wegnehmen.
// - U-Wert: Pset_WallCommon/Pset_SlabCommon/Pset_RoofCommon.ThermalTransmittance → Aufbau.uWert (ausdrücklich, Quelle Datei +
//   Pset), wenn alle Bauteile desselben Aufbaus denselben Wert nennen; Werte ≤ 0 gelten als «nicht berechnet».
//   Pset_WindowCommon.ThermalTransmittance → Fenster.uw, Pset_DoorCommon.ThermalTransmittance → Tuer.ud,
//   GlazingAreaFraction f → Fenster.rahmenanteil = 1 − f. IsExternal ist nur ein Hinweis (die Angrenzung rechnet der Kern).
// - Nordwinkel: TrueNorth des Modell-Kontexts (Richtung in den Achsen der Datei), Winkel von Projekt-Nord (+y) zu geografisch
//   Nord im Uhrzeigersinn = 90° − (Richtung der Nord in Projektachsen). Fehlt TrueNorth, gilt die Georeferenz
//   (IfcMapConversion). Gesetzt wird nur, wenn nordwinkel noch leer ist.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Clipper2Lib;
using Xbim.Common;
using Xbim.Ifc4.Interfaces;

namespace Hlks.Kern;

public sealed class IfcHuelle : IIfcUebertrag
{
    public string Bereich => "architektur";
    public IReadOnlySet<string> Klassen { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Material", "Aufbau" };

    public void Uebertragen(IfcLauf lauf) => new IfcHuelleLauf(lauf).Ausfuehren();
}

sealed class IfcHuelleLauf(IfcLauf lauf)
{
    readonly IfcDatei d = lauf.Datei;
    readonly IfcSammler hinweise = new();
    readonly Dictionary<int, IIfcMaterialSelect> materialVon = [];
    readonly Dictionary<string, IIfcBuildingElement> produkte = new(StringComparer.Ordinal);
    /// <summary>Genutzte Materialien nach Name (Reihenfolge des ersten Auftretens).</summary>
    readonly Dictionary<string, List<IIfcMaterial>> materialien = new(StringComparer.Ordinal);
    readonly Dictionary<string, HashSet<string>> fremdeKennungen = new(StringComparer.Ordinal);
    readonly Dictionary<string, List<(double U, string Fundstelle, Element Bauteil)>> aufbauU = [];
    readonly Dictionary<IfcDerivedUnitEnum, double?> einheiten = [];
    int bauteileMitU, bauteileNurU, aussenJa, aussenNein;

    static double Zahl(IfcWert? w) => w?.Inhalt switch { double x => x, long l => l, int i => i, _ => double.NaN };

    public void Ausfuehren()
    {
        var bauteile = lauf.Geplant.Values.Where(e => lauf.Katalog.IstArt(e, "Bauteil") && e.Kennungen?.ContainsKey("ifc") == true).ToList();
        if (bauteile.Count > 0)
        {
            Vorbereiten();
            foreach (var e in bauteile) Bauteil(e);
            AufbauUWerte();
        }
        GerechnetesBehalten();
        int nAufbau = lauf.Geplant.Values.Count(e => e.Klasse == "Aufbau");
        int nAufbauNurU = lauf.Geplant.Values.Count(e => e.Klasse == "Aufbau" && e.Kennungen!["ifc"].StartsWith("aufbau:u:"));
        var (nMaterial, nLambda) = MaterialienPlanen();
        Nordwinkel();
        if (aussenJa + aussenNein > 0)
            lauf.Hinweise.Add($"Hülle: IsExternal in der Datei: {aussenJa} Bauteile aussen, {aussenNein} innen – nur Hinweis, die Angrenzung rechnet der Kern aus der Geometrie.");
        hinweise.Schreiben(lauf.Hinweise, "Hülle");
        lauf.Hinweise.Insert(0, $"Hülle: {nAufbau} Aufbauten, {nMaterial} Materialien ({nLambda} mit λ), {bauteileMitU} Bauteile mit U aus der Datei, davon {bauteileNurU} nur mit U aus der Datei ({nAufbauNurU} Aufbauten ohne Schichten)");
    }

    /// <summary>Gerechnete Werte (Herkunft «berechnung», z. B. rWert aus der Nachführung der Hülle), die ein früherer Übertrag
    /// schon am Aufbau hinterlassen hat, bleiben im Plan stehen. Sonst sähe der Abgleich bei jeder erneuten Übernahme
    /// «geändert» (der Plan kennt sie nicht), und es entstünde jedes Mal ein Paket. Ändert sich der Aufbau oder ein Material,
    /// rechnet die Nachführung im selben Paket neu (Befund B4-2).</summary>
    void GerechnetesBehalten()
    {
        var bestand = new Dictionary<string, Element>(StringComparer.Ordinal);
        foreach (var e in lauf.Kontext.Arbeit.DerKlasse("Aufbau"))
            if (e.Kennungen is { } k && k.GetValueOrDefault("quelle") == lauf.UnterlageId && k.GetValueOrDefault("ifc") is { } kn)
                bestand.TryAdd(kn, e);
        foreach (var plan in lauf.Geplant.Values.Where(x => x.Klasse == "Aufbau"))
            if (bestand.TryGetValue(plan.Kennungen!["ifc"], out var alt))
                foreach (var (name, w) in alt.Kopie().Eigenschaften)
                    if (w.Herkunft.Art == "berechnung" && !plan.Eigenschaften.ContainsKey(name))
                        plan.Eigenschaften[name] = w;
    }

    void Vorbereiten()
    {
        foreach (var r in d.Modell.Instances.OfType<IIfcRelAssociatesMaterial>())
            foreach (var o in r.RelatedObjects)
                if (o is IPersistEntity pe) materialVon.TryAdd(pe.EntityLabel, r.RelatingMaterial);
        foreach (var b in d.Modell.Instances.OfType<IIfcBuildingElement>())
            produkte.TryAdd(b.GlobalId.ToString(), b);
        // Kennungen anderer Unterlagen (Schutz vor Wegnehmen beim Abgleich)
        foreach (var e in lauf.Kontext.Arbeit.Elemente)
            if (e.Kennungen?.GetValueOrDefault("ifc") is { } k && (k.StartsWith("material:") || k.StartsWith("aufbau:")))
            {
                if (!fremdeKennungen.TryGetValue(k, out var q)) fremdeKennungen[k] = q = [];
                q.Add(e.Kennungen.GetValueOrDefault("quelle") ?? "");
            }
    }

    /// <summary>Kennung eines Materials oder Aufbaus; mit Anhang, wenn sie schon zu einer anderen Unterlage gehört.</summary>
    string Kennung(string basis)
    {
        if (fremdeKennungen.TryGetValue(basis, out var q) && q.Any(x => x != lauf.UnterlageId))
            return $"{basis}@{lauf.UnterlageId[..Math.Min(8, lauf.UnterlageId.Length)]}";
        return basis;
    }

    IIfcMaterialSelect? MaterialVon(IIfcObject o) =>
        materialVon.GetValueOrDefault(o.EntityLabel) ?? (d.Typ(o) is { } t ? materialVon.GetValueOrDefault(t.EntityLabel) : null);

    static string MaterialName(IIfcMaterial m) => IfcLauf.Text(m.Name) ?? $"IfcMaterial #{m.EntityLabel}";

    /// <summary>Alle IfcMaterial hinter einer Materialangabe (Schichtaufbau, Material, Liste, Konstituenten, Profile).</summary>
    static IEnumerable<IIfcMaterial> MaterialienVon(IIfcMaterialSelect? s)
    {
        switch (s)
        {
            case IIfcMaterial m: yield return m; break;
            case IIfcMaterialLayer l: if (l.Material is { } lm) yield return lm; break;
            case IIfcMaterialLayerSetUsage u:
                foreach (var x in MaterialienVon(u.ForLayerSet)) yield return x;
                break;
            case IIfcMaterialLayerSet ls:
                foreach (var l in ls.MaterialLayers) if (l.Material is { } m2) yield return m2;
                break;
            case IIfcMaterialList ml:
                foreach (var x in ml.Materials) yield return x;
                break;
            case IIfcMaterialConstituentSet cs:
                foreach (var c in cs.MaterialConstituents) if (c.Material is { } m3) yield return m3;
                break;
            case IIfcMaterialProfileSetUsage pu:
                foreach (var x in MaterialienVon(pu.ForProfileSet)) yield return x;
                break;
            case IIfcMaterialProfileSet ps:
                foreach (var p in ps.MaterialProfiles) if (p.Material is { } m4) yield return m4;
                break;
        }
    }

    void Nutze(IIfcMaterial m)
    {
        var n = MaterialName(m);
        if (!materialien.TryGetValue(n, out var l)) materialien[n] = l = [];
        if (!l.Any(x => x.EntityLabel == m.EntityLabel)) l.Add(m);
    }

    // ------------------------------------------------------------------ Bauteile

    void Bauteil(Element e)
    {
        var gid = e.Kennungen!["ifc"];
        var kopf = gid.Contains(':') ? gid[..gid.IndexOf(':')] : gid;
        if (!produkte.TryGetValue(kopf, out var p)) return;
        var sel = MaterialVon(p);
        foreach (var m in MaterialienVon(sel)) Nutze(m);
        var eig = d.Eigenschaften(p);
        string ent = $"{IfcDatei.Entitaet(p)} #{p.EntityLabel}";

        if (e.Klasse == "Decke" && d.PredefinedType(p) == "BASESLAB")
            lauf.Wert(e, "art", "boden", $"{ent}, PredefinedType BASESLAB");

        Element? aufbau = null;
        if (sel is IIfcMaterialLayerSetUsage or IIfcMaterialLayerSet)
        {
            if (e.Klasse is "Wand" or "Decke" or "Dach" or "Tuer") aufbau = AufbauPlanen(e, p, sel, ent);
            else hinweise.Dazu($"{e.Klasse} mit Schichtaufbau: kein Aufbau (Aufbau gibt es für Wand, Decke, Dach, Tür)", IfcSammler.Beispiel(p));
            if (aufbau is not null) e.Bezuege["aufbau"] = aufbau.Id;
        }

        var psets = e.Klasse switch
        {
            "Wand" => new[] { "Pset_WallCommon" },
            "Decke" => ["Pset_SlabCommon"],
            "Dach" => ["Pset_RoofCommon", "Pset_SlabCommon"],
            "Fenster" => ["Pset_WindowCommon"],
            "Tuer" => ["Pset_DoorCommon"],
            _ => Array.Empty<string>(),
        };
        if (psets.Length == 0) return;

        if (e.Klasse is "Wand" or "Decke" or "Dach" && IfcDatei.Erster(eig, psets.Select(s => s + ".IsExternal")) is { Inhalt: bool aussen })
        {
            if (aussen) aussenJa++; else aussenNein++;
        }

        var uw = IfcDatei.Erster(eig, psets.Select(s => s + ".ThermalTransmittance"));
        if (uw is not null && Zahl(uw) is double roh)
        {
            var pset = psets.First(s => eig.ContainsKey(s + ".ThermalTransmittance"));
            string fund = $"{pset}.ThermalTransmittance ({ent})";
            if (double.IsNaN(roh) || roh <= 0)
                hinweise.Dazu("ThermalTransmittance 0 oder negativ (nicht berechnet) – nicht übernommen, Bauteile", IfcSammler.Beispiel(p));
            else if (Faktor(IfcDerivedUnitEnum.THERMALTRANSMITTANCEUNIT, "ThermalTransmittance") is not double f)
                hinweise.Dazu("ThermalTransmittance in einer Einheit, die nicht umrechenbar ist – nicht übernommen, Bauteile", IfcSammler.Beispiel(p));
            else
            {
                double u = Math.Round(roh * f, 6);
                switch (e.Klasse)
                {
                    case "Fenster": lauf.Wert(e, "uw", u, fund); bauteileMitU++; break;
                    case "Tuer": lauf.Wert(e, "ud", u, fund); bauteileMitU++; break;
                    default:
                        if (aufbau is null)
                        {
                            // Entscheid Hauptlauf 18.10.2026: ohne Schichtaufbau ein Aufbau ohne Schichten, der nur das ausdrückliche U trägt
                            if (UAufbau(e, p, u, fund) is { } nurU)
                            {
                                e.Bezuege["aufbau"] = nurU.Id;
                                bauteileMitU++;
                                bauteileNurU++;
                            }
                            else hinweise.Dazu("ThermalTransmittance auf drei Stellen 0 – nicht übernommen, Bauteile", IfcSammler.Beispiel(p));
                        }
                        else
                        {
                            if (!aufbauU.TryGetValue(aufbau.Id, out var l)) aufbauU[aufbau.Id] = l = [];
                            l.Add((u, fund, e));
                        }
                        break;
                }
            }
        }

        if (e.Klasse == "Fenster" && IfcDatei.Erster(eig, ["Pset_WindowCommon.GlazingAreaFraction"]) is { } gl && Zahl(gl) is double fr)
        {
            if (fr > 0 && fr <= 1) lauf.Wert(e, "rahmenanteil", Math.Round(1 - fr, 6), $"1 − Pset_WindowCommon.GlazingAreaFraction ({ent})");
            else hinweise.Dazu("GlazingAreaFraction ausserhalb von 0 … 1 (0 = nicht angegeben) – rahmenanteil nicht gesetzt, Fenster", IfcSammler.Beispiel(p));
        }
    }

    // ------------------------------------------------------------------ Aufbau

    sealed record Schicht(IIfcMaterial Material, string Name, double Dicke);

    Element? AufbauPlanen(Element e, IIfcBuildingElement p, IIfcMaterialSelect sel, string ent)
    {
        var usage = sel as IIfcMaterialLayerSetUsage;
        var set = usage?.ForLayerSet ?? (IIfcMaterialLayerSet)sel;
        var roh = new List<Schicht>();
        bool ohneMaterial = false;
        foreach (var l in set.MaterialLayers)
        {
            double dicke = (double)l.LayerThickness * d.MmJeEinheit;
            if (l.Material is null) { if (dicke > 0) ohneMaterial = true; continue; }
            if (double.IsNaN(dicke) || dicke <= 0) { hinweise.Dazu("Schicht mit Dicke 0 im Schichtaufbau weggelassen", IfcSammler.Beispiel(p)); continue; }
            roh.Add(new Schicht(l.Material, MaterialName(l.Material), Math.Round(dicke, 1)));
        }
        if (ohneMaterial) { hinweise.Dazu("Schichtaufbau mit einer Schicht ohne Material – kein Aufbau", IfcSammler.Beispiel(p)); return null; }
        if (roh.Count == 0) { hinweise.Dazu("Schichtaufbau ohne Schicht mit Dicke – kein Aufbau", IfcSammler.Beispiel(p)); return null; }

        bool? umkehren = Umkehren(e, p, usage, out string grund);
        if (umkehren is null) hinweise.Dazu($"Schichtfolge in der Reihenfolge der Datei übernommen ({grund})", IfcSammler.Beispiel(p));
        var schichten = umkehren == true ? Enumerable.Reverse(roh).ToList() : roh;

        string art = Art(e.Klasse, p);
        var text = art + "|" + string.Join("|", schichten.Select(s => $"{s.Name}:{IfcBauteile.Mm(s.Dicke)}"));
        var summe = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..12].ToLowerInvariant();
        var kennung = Kennung("aufbau:" + summe);
        var id = "ifc:" + kennung;
        if (lauf.Geplant.TryGetValue(id, out var da)) return da;

        var mengenName = IfcLauf.Text(set.LayerSetName);
        var fund = $"{IfcDatei.Entitaet(set)} #{set.EntityLabel}" + (mengenName is null ? "" : $" «{mengenName}»");
        var a = new Element
        {
            Id = id,
            Klasse = "Aufbau",
            Name = string.Join(" / ", schichten.Select(s => $"{s.Name} {IfcBauteile.Mm(s.Dicke)}")),
            Kennungen = new() { ["ifc"] = kennung, ["quelle"] = lauf.UnterlageId },
            Angelegt = lauf.Herkunft with { Quelle = $"{lauf.Quelle}, {fund}" },
        };
        lauf.Geplant[id] = a;
        lauf.Wert(a, "art", art, fund);
        lauf.Wert(a, "schichten", new JsonArray(schichten.Select(s => (JsonNode)new JsonObject
        {
            ["material"] = "ifc:" + Kennung("material:" + s.Name),
            ["dicke"] = s.Dicke,
        }).ToArray()), $"{fund}, {schichten.Count} Schichten innen → aussen");
        return a;
    }

    string Art(string klasse, IIfcBuildingElement p) => klasse switch
    {
        "Wand" => "wand",
        "Dach" => "dach",
        "Tuer" => "tuer",
        _ => d.PredefinedType(p) == "BASESLAB" ? "boden" : "decke",
    };

    /// <summary>Aufbau ohne Schichten, der nur das ausdrückliche U der Datei trägt (Bauteil ohne Schichtaufbau). Kennung
    /// «aufbau:u:&lt;art&gt;:&lt;U auf 3 Stellen&gt;», gleiche Art und gleiches (auf 3 Stellen gerundetes) U = derselbe Aufbau;
    /// uWert ist das auf 3 Stellen gerundete U. null, wenn U gerundet 0 ist.</summary>
    Element? UAufbau(Element e, IIfcBuildingElement p, double u, string fund)
    {
        double ur = Math.Round(u, 3);
        if (ur <= 0) return null;
        string art = Art(e.Klasse, p);
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var kennung = Kennung($"aufbau:u:{art}:{ur.ToString("0.000", inv)}");
        var id = "ifc:" + kennung;
        if (lauf.Geplant.TryGetValue(id, out var da)) return da;
        var a = new Element
        {
            Id = id,
            Klasse = "Aufbau",
            Name = $"U aus Datei {ur.ToString("0.###", inv)} ({art})",
            Kennungen = new() { ["ifc"] = kennung, ["quelle"] = lauf.UnterlageId },
            Angelegt = lauf.Herkunft with { Quelle = $"{lauf.Quelle}, {fund}" },
        };
        lauf.Geplant[id] = a;
        lauf.Wert(a, "art", art, fund);
        lauf.Wert(a, "uWert", ur, fund + ", auf drei Stellen gerundet, Bauteil ohne Schichtaufbau");
        return a;
    }

    /// <summary>true = die Schichten der Datei müssen umgedreht werden, damit sie innen → aussen laufen; false = Reihenfolge der
    /// Datei ist innen → aussen; null = nicht bestimmbar (Reihenfolge der Datei, grund nennt warum).</summary>
    bool? Umkehren(Element e, IIfcBuildingElement p, IIfcMaterialLayerSetUsage? usage, out string grund)
    {
        grund = "";
        if (e.Klasse == "Tuer") { grund = "Tür"; return null; }
        if (usage is null) { grund = "Schichtrichtung unbekannt (nur IfcMaterialLayerSet)"; return null; }
        var a = Affin.Aus(d.Lage(p));
        double[] achse = usage.LayerSetDirection switch
        {
            IfcLayerSetDirectionEnum.AXIS1 => a.AchseX,
            IfcLayerSetDirectionEnum.AXIS2 => a.AchseY,
            _ => a.AchseZ,
        };
        double vorzeichen = usage.DirectionSense == IfcDirectionSenseEnum.NEGATIVE ? -1 : 1;
        double l = Math.Sqrt(achse[0] * achse[0] + achse[1] * achse[1] + achse[2] * achse[2]);
        if (l < 1e-9) { grund = "Lage ohne Achsen"; return null; }
        double sx = vorzeichen * achse[0] / l, sy = vorzeichen * achse[1] / l, sz = vorzeichen * achse[2] / l;

        if (e.Klasse == "Wand")
        {
            if (Math.Abs(sz) > 0.5 || Math.Sqrt(sx * sx + sy * sy) < 0.5) { grund = "Schichtrichtung der Wand nicht waagrecht"; return null; }
            if (Aussenrichtung(e) is not { } au) { grund = "Aussenseite der Wand nicht bestimmbar (Innenwand oder Hülle nicht geschlossen)"; return null; }
            return sx * au[0] + sy * au[1] < 0;
        }
        if (Math.Abs(sz) < 0.5) { grund = $"Schichtrichtung der {e.Klasse} nicht senkrecht"; return null; }
        // Decke, Boden: Raumseite oben, erste Schicht = innen, wenn die Folge nach unten läuft; Dach: Raumseite unten
        return e.Klasse == "Dach" ? sz < 0 : sz > 0;
    }

    // ------------------------------------------------------------------ Aussenseite einer Wand

    /// <summary>Einheitsvektor von der Wand nach aussen (in der Ebene) oder null: aussen ist die Seite, auf der kein
    /// umschlossener Raum liegt, wenn auf der anderen Seite einer liegt.</summary>
    double[]? Aussenrichtung(Element w)
    {
        var g = w.Geometrie;
        if (g is null || g.Punkte.Count < 2 || w.Geschoss is null || g.Dicke is not double dicke || dicke <= 0) return null;
        var ringe = Ringe(w.Geschoss);
        if (ringe.Count == 0) return null;
        var a = g.Punkte[0]; var b = g.Punkte[^1];
        double dx = b[0] - a[0], dy = b[1] - a[1], l = Math.Sqrt(dx * dx + dy * dy);
        if (l < 1) return null;
        double nx = -dy / l, ny = dx / l, abstand = dicke / 2 + 100;
        int links = 0, rechts = 0;
        foreach (var t in new[] { 0.25, 0.5, 0.75 })
        {
            double mx = a[0] + dx * t, my = a[1] + dy * t;
            if (ringe.Any(r => Polygone.ImRing(r, mx + nx * abstand, my + ny * abstand))) links++;
            if (ringe.Any(r => Polygone.ImRing(r, mx - nx * abstand, my - ny * abstand))) rechts++;
        }
        if (links > 0 && rechts == 0) return [-nx, -ny];
        if (rechts > 0 && links == 0) return [nx, ny];
        return null;
    }

    /// <summary>Umschlossene Flächen (Löcher der Vereinigung aller Wandbänder) des Geschosses, ab 1 m².</summary>

    List<List<double[]>> Ringe(string geschoss)
    {
        if (ringeCache.TryGetValue(geschoss, out var da)) return da;
        var lagen = lauf.Geplant.Values
            .Where(x => x.Klasse == "Wand" && x.Geschoss == geschoss && x.Geometrie is { Punkte.Count: >= 2 })
            .Select(x => Wandgeometrie.Lage(lauf.Kontext.Arbeit, x)).OfType<Wandlage>().Where(l => l.Dicke > 0).ToList();
        var ringe = new List<List<double[]>>();
        if (lagen.Count > 0)
        {
            Wandgeometrie.Verlaengern(lagen);
            var baum = Polygone.Baum(new PathsD(lagen.Select(l => l.Band())));
            void Sammeln(PolyPathD knoten)
            {
                for (int i = 0; i < knoten.Count; i++)
                {
                    var kind = knoten[i];
                    if (kind.IsHole && Math.Abs(Clipper.Area(kind.Polygon!)) >= IfcRaeume.KleinsteFlaeche) ringe.Add(Polygone.Ring(kind.Polygon!, true));
                    Sammeln(kind);
                }
            }
            Sammeln(baum);
        }
        return ringeCache[geschoss] = ringe;
    }

    readonly Dictionary<string, List<List<double[]>>> ringeCache = [];

    // ------------------------------------------------------------------ U-Werte der Aufbauten

    void AufbauUWerte()
    {
        foreach (var (id, liste) in aufbauU)
        {
            var a = lauf.Geplant[id];
            var werte = liste.Select(x => x.U).Distinct().ToList();
            if (werte.Max() - werte.Min() > 1e-6)
            {
                hinweise.Dazu("Bauteile mit gleichem Aufbau nennen verschiedene U-Werte – uWert nicht gesetzt, Aufbauten",
                    $"«{a.Name}»: {string.Join(" / ", werte.Take(4).Select(w => w.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)))}");
                continue;
            }
            var fund = liste[0].Fundstelle + (liste.Count > 1 ? $" und {liste.Count - 1} weitere Bauteile mit demselben Aufbau" : "");
            lauf.Wert(a, "uWert", werte[0], fund);
            bauteileMitU += liste.Count;
        }
    }

    // ------------------------------------------------------------------ Materialien

    (int Anzahl, int MitLambda) MaterialienPlanen()
    {
        int mitLambda = 0;
        foreach (var (name, quellen) in materialien)
        {
            var kennung = Kennung("material:" + name);
            var id = "ifc:" + kennung;
            if (lauf.Geplant.ContainsKey(id)) continue;
            var erste = quellen[0];
            var e = new Element
            {
                Id = id,
                Klasse = "Material",
                Name = name,
                Kennungen = new() { ["ifc"] = kennung, ["quelle"] = lauf.UnterlageId },
                Angelegt = lauf.Herkunft with { Quelle = $"{lauf.Quelle}, IfcMaterial #{erste.EntityLabel}" },
            };
            lauf.Geplant[id] = e;
            var k = Kennwerte(name, quellen);
            if (k.Lambda is { } l) { lauf.Wert(e, "lambda", l.Wert, l.Fundstelle); mitLambda++; }
            if (k.C is { } c) lauf.Wert(e, "waermekapazitaet", c.Wert, c.Fundstelle);
            if (k.Rho is { } r) lauf.Wert(e, "rohdichte", r.Wert, r.Fundstelle);
        }
        return (materialien.Count, mitLambda);
    }

    sealed record Fund(double Wert, string Fundstelle);

    (Fund? Lambda, Fund? C, Fund? Rho) Kennwerte(string name, List<IIfcMaterial> quellen)
    {
        Fund? lambda = null, c = null, rho = null;
        void Dazu(ref Fund? ziel, string was, IIfcMaterial m, string pset, string eigenschaft, string stelle, double roh, IfcDerivedUnitEnum einheit)
        {
            if (double.IsNaN(roh) || double.IsInfinity(roh) || roh <= 0)
            {
                hinweise.Dazu($"{was} 0 oder negativ – nicht übernommen, Materialien", $"«{name}»");
                return;
            }
            if (Faktor(einheit, was) is not double f)
            {
                hinweise.Dazu($"{was} in einer Einheit, die nicht umrechenbar ist – nicht übernommen, Materialien", $"«{name}»");
                return;
            }
            var neu = new Fund(roh * f, $"{pset}.{eigenschaft}, {stelle}, IfcMaterial #{m.EntityLabel} «{name}»");
            if (ziel is null) ziel = neu;
            else if (Math.Abs(ziel.Wert - neu.Wert) > 1e-9 * Math.Max(1, Math.Abs(neu.Wert)))
                hinweise.Dazu($"Material mit gleichem Namen, aber verschiedenem {was} in der Datei – erster Wert übernommen, Materialien", $"«{name}»");
        }
        foreach (var m in quellen)
            foreach (var mp in m.HasProperties)
            {
                string stelle = $"{IfcDatei.Entitaet((IPersistEntity)mp)} #{((IPersistEntity)mp).EntityLabel}";
                // IFC2x3: die Schnittstellen-Eigenschaften (Name, Properties) von IfcMaterialProperties liefern in xBIM nichts bzw. werfen – die eigenen lesen
                var pset = (mp is Xbim.Ifc2x3.MaterialPropertyResource.IfcExtendedMaterialProperties xn ? IfcLauf.Text(xn.Name) : IfcLauf.Text(mp.Name)) ?? "";
                // IFC2x3: eigene Entitäten für thermische und allgemeine Kennwerte
                if (mp is Xbim.Ifc2x3.MaterialPropertyResource.IfcThermalMaterialProperties t2)
                {
                    if (t2.ThermalConductivity is { } tc) Dazu(ref lambda, "ThermalConductivity", m, "IfcThermalMaterialProperties", "ThermalConductivity", stelle, (double)tc, IfcDerivedUnitEnum.THERMALCONDUCTANCEUNIT);
                    if (t2.SpecificHeatCapacity is { } sc) Dazu(ref c, "SpecificHeatCapacity", m, "IfcThermalMaterialProperties", "SpecificHeatCapacity", stelle, (double)sc, IfcDerivedUnitEnum.SPECIFICHEATCAPACITYUNIT);
                    continue;
                }
                if (mp is Xbim.Ifc2x3.MaterialPropertyResource.IfcGeneralMaterialProperties g2)
                {
                    if (g2.MassDensity is { } md) Dazu(ref rho, "MassDensity", m, "IfcGeneralMaterialProperties", "MassDensity", stelle, (double)md, IfcDerivedUnitEnum.MASSDENSITYUNIT);
                    continue;
                }
                bool thermisch = pset.Equals("Pset_MaterialThermal", StringComparison.OrdinalIgnoreCase);
                bool allgemein = pset.Equals("Pset_MaterialCommon", StringComparison.OrdinalIgnoreCase);
                if (!thermisch && !allgemein) continue;
                // IFC2x3: IfcExtendedMaterialProperties (Revit-Art, Name = Pset-Name) trägt die Werte in ExtendedProperties
                var eigenschaften = mp is Xbim.Ifc2x3.MaterialPropertyResource.IfcExtendedMaterialProperties x2
                    ? x2.ExtendedProperties.OfType<IIfcPropertySingleValue>() : mp.Properties.OfType<IIfcPropertySingleValue>();
                foreach (var pr in eigenschaften)
                {
                    var pn = IfcLauf.Text(pr.Name);
                    if (pr.NominalValue?.Value is not { } wert) continue;
                    double z;
                    try { z = Convert.ToDouble(wert, System.Globalization.CultureInfo.InvariantCulture); } catch { continue; }
                    if (thermisch && "ThermalConductivity".Equals(pn, StringComparison.OrdinalIgnoreCase))
                        Dazu(ref lambda, "ThermalConductivity", m, pset, "ThermalConductivity", stelle, z, IfcDerivedUnitEnum.THERMALCONDUCTANCEUNIT);
                    else if (thermisch && "SpecificHeatCapacity".Equals(pn, StringComparison.OrdinalIgnoreCase))
                        Dazu(ref c, "SpecificHeatCapacity", m, pset, "SpecificHeatCapacity", stelle, z, IfcDerivedUnitEnum.SPECIFICHEATCAPACITYUNIT);
                    else if (allgemein && "MassDensity".Equals(pn, StringComparison.OrdinalIgnoreCase))
                        Dazu(ref rho, "MassDensity", m, pset, "MassDensity", stelle, z, IfcDerivedUnitEnum.MASSDENSITYUNIT);
                }
            }
        return (lambda is null ? null : lambda with { Wert = Math.Round(lambda.Wert, 9) },
                c is null ? null : c with { Wert = Math.Round(c.Wert, 6) },
                rho is null ? null : rho with { Wert = Math.Round(rho.Wert, 6) });
    }

    // ------------------------------------------------------------------ Einheiten

    /// <summary>Faktor, mit dem ein Wert der Datei in die SI-Einheit (W/(m·K), J/(kg·K), kg/m³, W/(m²·K)) umgerechnet wird.
    /// Hat die Datei keine abgeleitete Einheit dieser Art, gilt SI (Vorgabe im IFC-Standard). null = nicht umrechenbar. Die
    /// Dimension der abgeleiteten Einheit wird nicht geprüft.</summary>
    double? Faktor(IfcDerivedUnitEnum art, string was)
    {
        if (einheiten.TryGetValue(art, out var da)) return da;
        double? f = 1;
        var einheit = d.Modell.Instances.FirstOrDefault<IIfcProject>()?.UnitsInContext?.Units.OfType<IIfcDerivedUnit>().FirstOrDefault(u => u.UnitType == art);
        if (einheit is not null)
        {
            f = 1;
            foreach (var el in einheit.Elements)
            {
                var x = el.Unit is null ? null : NamedFaktor(el.Unit);
                if (x is null) { f = null; break; }
                f *= Math.Pow(x.Value, (double)el.Exponent);
            }
            if (f is double ff && Math.Abs(ff - 1) > 1e-12)
                lauf.Hinweise.Add($"Hülle: Einheit {art} der Datei weicht von SI ab, {was} mit Faktor {ff.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture)} umgerechnet.");
        }
        return einheiten[art] = f;
    }

    static double? NamedFaktor(IIfcNamedUnit u)
    {
        switch (u)
        {
            case IIfcSIUnit si:
                if (si.UnitType == IfcUnitEnum.THERMODYNAMICTEMPERATUREUNIT && si.Name.ToString() is not ("KELVIN" or "DEGREE_CELSIUS")) return null;
                double prefix = si.Prefix?.ToString() switch
                {
                    null => 1,
                    "EXA" => 1e18, "PETA" => 1e15, "TERA" => 1e12, "GIGA" => 1e9, "MEGA" => 1e6, "KILO" => 1e3, "HECTO" => 1e2, "DECA" => 10,
                    "DECI" => 1e-1, "CENTI" => 1e-2, "MILLI" => 1e-3, "MICRO" => 1e-6, "NANO" => 1e-9, "PICO" => 1e-12, "FEMTO" => 1e-15, "ATTO" => 1e-18,
                    _ => double.NaN,
                };
                if (double.IsNaN(prefix)) return null;
                return prefix * (si.Name.ToString() == "GRAM" ? 1e-3 : 1);
            case IIfcConversionBasedUnit cb:
                if (cb.UnitType == IfcUnitEnum.THERMODYNAMICTEMPERATUREUNIT) return null;   // Fahrenheit u. ä.: nicht nur ein Faktor
                if (cb.ConversionFactor?.UnitComponent is IIfcNamedUnit basis && NamedFaktor(basis) is double bf
                    && cb.ConversionFactor.ValueComponent?.Value is { } v)
                {
                    try { return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture) * bf; } catch { return null; }
                }
                return null;
            default: return null;
        }
    }

    // ------------------------------------------------------------------ Nordrichtung

    void Nordwinkel()
    {
        var k = lauf.Kontext;
        double? winkel = null; string? stelle = null;
        var ctx = d.Modell.Instances.OfType<IIfcGeometricRepresentationContext>()
            .Where(c => c is not IIfcGeometricRepresentationSubContext && c.TrueNorth is not null)
            .OrderBy(c => string.Equals(IfcLauf.Text(c.ContextType), "Model", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(c => c.EntityLabel).FirstOrDefault();
        double drehung = d.Bezug.Drehung;
        if (ctx?.TrueNorth is { } tn)
        {
            var r = tn.DirectionRatios.Select(x => (double)x).ToArray();
            if (r.Length >= 2 && (Math.Abs(r[0]) > 1e-12 || Math.Abs(r[1]) > 1e-12))
            {
                double alpha = Math.Atan2(r[1], r[0]) * 180 / Math.PI;
                winkel = Normieren(90 - (alpha - drehung));
                stelle = $"{IfcDatei.Entitaet(ctx)} #{ctx.EntityLabel}.TrueNorth ({Zahl3(r[0])}, {Zahl3(r[1])}), Ausrichtung der Datei {Zahl3(drehung)}°";
            }
        }
        if (winkel is null && d.Karte() is { } karte)
        {
            winkel = Normieren(karte.Drehung + drehung);
            stelle = $"{karte.Herkunft}, Drehung {Zahl3(karte.Drehung)}° + Ausrichtung der Datei {Zahl3(drehung)}° (keine TrueNorth)";
        }
        if (winkel is not double w)
        {
            lauf.Hinweise.Add("Hülle: Die Datei nennt keine Nordrichtung (TrueNorth, Georeferenz) – nordwinkel nicht gesetzt (Projekt-Nord = +y).");
            return;
        }
        w = Math.Round(w, 3);
        var info = k.Arbeit.DerKlasse("Projektinformation").FirstOrDefault();
        if (info?.Zahl("nordwinkel") is double bisher)
        {
            lauf.Hinweise.Add(Math.Abs(Normieren(bisher - w)) > 0.5
                ? $"Hülle: nordwinkel aus der Datei: {Zahl3(w)}° – weicht vom Projekt ({Zahl3(bisher)}°) ab, Projektwert bleibt."
                : $"Hülle: nordwinkel aus der Datei: {Zahl3(w)}° – stimmt mit dem Projekt überein.");
            return;
        }
        var e = info?.Kopie() ?? new Element { Id = Guid.NewGuid().ToString(), Kennzeichen = k.NaechstesKennzeichen("PI"), Klasse = "Projektinformation", Name = "Projektinformation" };
        lauf.Wert(e, "nordwinkel", w, stelle);
        k.Setzen(e);
        lauf.Hinweise.Add($"Hülle: nordwinkel aus der Datei: {Zahl3(w)}° gesetzt ({stelle}).");
    }

    static double Normieren(double w)
    {
        w %= 360;
        if (w > 180) w -= 360;
        if (w <= -180) w += 360;
        return w;
    }

    static string Zahl3(double x) => Math.Round(x, 3).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
}
