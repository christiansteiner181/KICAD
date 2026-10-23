using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Quest App (Modul XR, P2): Modell als GLB je Geschoss am Beispielprojekt «MFH Musterweg» (fiktiv) und IFC-Unterlage an der synthetischen
/// Architektur-Datei (IfcArchitekturTestdatei): gültige Dateien, ein Netz je Material, Dreiecke wie die Einzelkörper, Zuordnung Dreieck → Element,
/// Farben wie die 3D-Ansicht, Bezugspunkt, Versionsschlüssel.</summary>
public class XrModellTests
{
    static readonly Projekt Beispiel = Beispielprojekt.Erzeugen(Kat, null);

    /// <summary>Schlüssel eines Netzes wie XrModell ihn bildet – hier unabhängig aus XrFarben nachgerechnet.</summary>
    static (string Gruppe, string Farbe, double Vr, double D3) Schluessel(Koerper k, Koerperteil t)
    {
        var d3 = XrFarben.Deckkraft3d(t);
        return (XrFarben.Gruppe(k.Ebene), XrFarben.Farbe(t), XrFarben.DeckkraftVr(k.Ebene, d3), d3);
    }

    static (string, string, double, double) Schluessel(GlbPruefer g, int mesh)
    {
        var e = g.Extras(mesh);
        return ((string)e["gruppe"]!, (string)e["farbe"]!, (double)e["deckkraft"]!, (double)e["deckkraft3d"]!);
    }

    [Fact]
    public void Beispiel_JedesGeschoss_GueltigeGlb_WenigeNetze_SzeneMitBezugUndGeschoss()
    {
        var s = XrModell.Stand(Beispiel);
        Assert.Equal(["UG", "EG", "1.OG"], s.Geschosse.Select(g => g.Geschoss.Kennzeichen));   // nach Kote
        Assert.Equal([-2700.0, 0, 2900], s.Geschosse.Select(g => g.Geschoss.KoteMm));
        Assert.Equal([0.0, 0, 0], s.Bezugspunkt);   // das Beispiel liegt beim Projekt-Nullpunkt
        Assert.All(s.Geschosse, g => Assert.Null(g.SchluesselUnterlage));
        int summe = 0;
        foreach (var g in s.Geschosse)
        {
            var r = XrModell.Erzeugen(s, g.Geschoss.Id, XrModell.TeilModell);
            var p = GlbPruefer.Pruefen(r.Daten);
            Assert.True(r.Netze is > 0 and < 50, $"{g.Geschoss.Kennzeichen}: {r.Netze} Netze");
            Assert.Equal(r.Netze, p.Meshes.Count);
            Assert.Equal(r.Netze, ((JsonArray)p.Gltf["materials"]!).Count);
            Assert.Equal(r.Netze, ((JsonArray)p.Gltf["nodes"]!).Count);
            Assert.Equal(g.Elemente, r.Elemente);
            summe += r.Netze;
            var sz = p.SzeneExtras;
            Assert.Equal(XrModell.Fassung, (int)sz["fassung"]!);
            Assert.Equal("modell", (string?)sz["teil"]);
            Assert.Equal(g.SchluesselModell, (string?)sz["schluessel"]);
            Assert.Equal(g.Geschoss.Id, (string?)sz["geschoss"]!["id"]);
            Assert.Equal(g.Geschoss.KoteMm, (double)sz["geschoss"]!["koteMm"]!);
            Assert.Equal(g.Geschoss.HoeheMm, (double?)sz["geschoss"]!["hoeheMm"]);
            Assert.Equal(g.Geschoss.Kennzeichen == "UG" ? 2700.0 : 2900.0, g.Geschoss.HoeheMm!.Value);
            Assert.Equal([0.0, 0, 0], ((JsonArray)sz["bezugspunktMm"]!).Select(x => (double)x!));
            Assert.Equal(r.Dreiecke, (long)sz["dreiecke"]!);
            Assert.Equal(Beispiel.Kopf.Id, (string?)sz["projekt"]);
            // undurchsichtige Netze zuerst
            var blend = Enumerable.Range(0, p.Meshes.Count).Select(i => (string?)p.Material(i)["alphaMode"] == "BLEND").ToList();
            Assert.Equal(blend.OrderBy(b => b), blend);
        }
        Assert.True(summe < 200, $"ganze Szene {summe} Netze");
    }

    [Fact]
    public void Beispiel_EinNetzJeMaterial_DreieckeWieEinzelkoerper_ZuordnungStimmt()
    {
        var s = XrModell.Stand(Beispiel);
        var koerper = Grafik.Koerper(Beispiel).ToDictionary(k => k.Id);
        foreach (var g in s.Geschosse)
        {
            var p = GlbPruefer.Pruefen(XrModell.Glb(s, g.Geschoss.Id, XrModell.TeilModell));
            var ids = Enumerable.Range(0, p.Meshes.Count).SelectMany(m => p.Zuordnung(m).Select(z => z.Id)).ToHashSet();
            // Anzahl Netze = Anzahl verschiedener Materialien (Gruppe, Farbe, Deckkraft VR und 3D) der Körperteile dieses Geschosses
            var materialien = ids.SelectMany(id => koerper[id].Teile.Where(t => t.Art != "netz").Select(t => Schluessel(koerper[id], t))).ToHashSet();
            Assert.Equal(materialien.Count, p.Meshes.Count);
            Assert.Equal(materialien, Enumerable.Range(0, p.Meshes.Count).Select(m => Schluessel(p, m)).ToHashSet());
            for (int m = 0; m < p.Meshes.Count; m++)
            {
                var key = Schluessel(p, m);
                var z = p.Zuordnung(m);
                // Bereiche lückenlos hintereinander, decken alle Dreiecke, je Element ein Bereich
                Assert.Equal(0, z[0].Ab);
                for (int i = 1; i < z.Count; i++) Assert.Equal(z[i - 1].Ab + z[i - 1].Anzahl, z[i].Ab);
                Assert.Equal(p.Dreiecke(m), z[^1].Ab + z[^1].Anzahl);
                Assert.Equal(z.Count, z.Select(x => x.Id).Distinct().Count());
                Assert.Equal(p.Dreiecke(m), (int)p.Extras(m)["dreiecke"]!);
                // Dreiecke eines Elements = seine Teile mit diesem Material, einzeln zerlegt – gleiche Zahl und gleiche Lage
                foreach (var (id, ab, anzahl, ebene) in z)
                {
                    var k = koerper[id];
                    Assert.Equal(k.Ebene, ebene);
                    var einzeln = new XrPuffer(s.Bezugspunkt);
                    foreach (var t in k.Teile.Where(t => t.Art != "netz" && Schluessel(k, t) == key)) XrNetzbau.Teil(t, einzeln);
                    Assert.Equal(einzeln.Dreiecke, anzahl);
                    var erwartet = einzeln.Netz("e", new GlbMaterial("e", "#777777", 1), null);
                    var ist = p.Dreieckspunkte(m, ab, anzahl);
                    for (int i = 0; i < ist.Count; i++)
                    {
                        int e = (int)erwartet.Indizes[i] * 3;
                        Assert.Equal([erwartet.Positionen[e], erwartet.Positionen[e + 1], erwartet.Positionen[e + 2]], ist[i].Select(x => (float)x).ToArray());
                    }
                }
            }
        }
    }

    [Fact]
    public void Beispiel_JederKoerperInGenauEinemGeschoss_MitGeschossDort_OhneNachHoehe()
    {
        var s = XrModell.Stand(Beispiel);
        var wo = new Dictionary<string, string>();
        foreach (var g in s.Geschosse)
        {
            var p = GlbPruefer.Pruefen(XrModell.Glb(s, g.Geschoss.Id, XrModell.TeilModell));
            foreach (var id in Enumerable.Range(0, p.Meshes.Count).SelectMany(m => p.Zuordnung(m).Select(z => z.Id)).Distinct())
                Assert.True(wo.TryAdd(id, g.Geschoss.Id), $"{id} in zwei Geschossen");
        }
        var mitTeilen = Grafik.Koerper(Beispiel).Where(k => k.Teile.Any(t => t.Art != "netz")).ToList();
        Assert.Equal(mitTeilen.Select(k => k.Id).Order(), wo.Keys.Order());
        Assert.Equal(s.Geschosse.Sum(g => g.Elemente), mitTeilen.Count);
        int ohne = 0;
        foreach (var k in mitTeilen)
        {
            var e = Beispiel.Modell.Finde(k.Id)!;
            if (e.Geschoss is { } gid && s.Geschosse.Any(g => g.Geschoss.Id == gid)) { Assert.Equal(gid, wo[k.Id]); continue; }
            ohne++;
            // ohne Geschoss: das höchste Geschoss mit Kote ≤ Unterkante + 1 mm, sonst das unterste
            var h = IfcExportGeometrie.Huelle(k.Teile)!.Value;
            var erwartet = s.Geschosse.LastOrDefault(g => g.Geschoss.KoteMm <= h.Min[2] + 1) ?? s.Geschosse[0];
            Assert.Equal(erwartet.Geschoss.Id, wo[k.Id]);
        }
        Assert.True(ohne > 0, "Beispiel ohne Element ohne Geschoss – Regel nach Höhe ungeprüft");
    }

    [Fact]
    public void Beispiel_FarbenUndDeckkraftWieDie3DAnsicht_InVrWaendeUndDeckenDicht()
    {
        var s = XrModell.Stand(Beispiel);
        var eg = s.Geschoss("EG")!;
        var p = GlbPruefer.Pruefen(XrModell.Glb(s, eg.Geschoss.Id, XrModell.TeilModell));
        int Netz(Func<JsonObject, bool> wo) => Enumerable.Range(0, p.Meshes.Count).Single(m => wo(p.Extras(m)));
        // Wände: Materialfarbe hell, in VR undurchsichtig, in der 3D-Übersicht 0.28
        var w = Netz(e => (string?)e["gruppe"] == "architektur" && (string?)e["farbe"] == "#cfd3d8");
        Assert.Equal(1, (double)p.Extras(w)["deckkraft"]!);
        Assert.Equal(0.28, (double)p.Extras(w)["deckkraft3d"]!);
        Assert.Equal("OPAQUE", (string?)p.Material(w)["alphaMode"]);
        Assert.Contains(p.Zuordnung(w), z => z.Id == Beispiel.Modell.Finde("W-EG-S")!.Id && z.Zusatz == "wand");
        // Räume: Farbe nach Nutzung (Kern), Deckkraft 0.15, durchscheinend
        var e01 = Beispiel.Modell.Finde("E.01")!;
        var r = Netz(e => (string?)e["gruppe"] == "raum" && ((JsonArray)e["elemente"]!["ids"]!).Any(x => (string?)x == e01.Id));
        Assert.Equal(ArchitekturGrafik.RaumFarbe(e01.Text("nutzung")), (string?)p.Extras(r)["farbe"]);
        Assert.Equal(0.15, (double)p.Extras(r)["deckkraft"]!);
        Assert.Equal("BLEND", (string?)p.Material(r)["alphaMode"]);
        Assert.Equal(0.15, (double)p.Material(r)["pbrMetallicRoughness"]!["baseColorFactor"]![3]!, 6);
        // Haustechnik: Farbe des Mediums wie der Körper des Kerns (Teil.Farbe), nicht die Vorgabe rohr
        var koerper = Grafik.Koerper(Beispiel).ToDictionary(k => k.Id);
        var leitungen = Enumerable.Range(0, p.Meshes.Count).Where(m => (string?)p.Extras(m)["gruppe"] == "hlks")
            .SelectMany(m => p.Zuordnung(m).Where(z => z.Zusatz == "leitung").Select(z => (m, z.Id))).ToList();
        Assert.NotEmpty(leitungen);
        Assert.Contains(leitungen, x => koerper[x.Id].Teile.Any(t => t.Farbe is { Length: 7 } f && f.ToLowerInvariant() == (string?)p.Extras(x.m)["farbe"] && f != "#777777"));
    }

    [Fact]
    public void Koordinaten_InDerGlb_WandeckeAnBekannterStelle()
    {
        // W-EG-S: Achse (0,0)→(12000,0) auf Kote 0, 350 dick, 2900 hoch → glTF x 0…12 (ungefähr, Anschlüsse), y 0…2.9, z = −y
        var s = XrModell.Stand(Beispiel);
        var p = GlbPruefer.Pruefen(XrModell.Glb(s, "EG", XrModell.TeilModell));
        var w = Beispiel.Modell.Finde("W-EG-S")!;
        var (m, z) = Enumerable.Range(0, p.Meshes.Count).SelectMany(i => p.Zuordnung(i).Where(x => x.Id == w.Id).Select(x => (i, x))).Single();
        var pts = p.Dreieckspunkte(m, z.Ab, z.Anzahl);
        var k = Grafik.Koerper(Beispiel, [w.Id]).Single();
        var h = IfcExportGeometrie.Huelle(k.Teile)!.Value;
        Assert.Equal(h.Min[0] / 1000, pts.Min(q => q[0]), 5);
        Assert.Equal(h.Max[0] / 1000, pts.Max(q => q[0]), 5);
        Assert.Equal(h.Min[2] / 1000, pts.Min(q => q[1]), 5);   // Höhe → y
        Assert.Equal(h.Max[2] / 1000, pts.Max(q => q[1]), 5);
        Assert.Equal(-h.Max[1] / 1000, pts.Min(q => q[2]), 5);  // Nord → −z
        Assert.Equal(-h.Min[1] / 1000, pts.Max(q => q[2]), 5);
    }

    [Fact]
    public void Versionsschluessel_AendertSichNurImGeschossDesElements_RueckgaengigStelltIhnWiederHer()
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        var vorher = XrModell.Stand(p).Geschosse.ToDictionary(g => g.Geschoss.Kennzeichen, g => g.SchluesselModell);
        Assert.Same(XrModell.Erfassen(p), XrModell.Erfassen(p));   // ohne Änderung derselbe Stand
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "E.01", ["eigenschaften"] = new JsonObject { ["lichteHoehe"] = 2400 } }));
        var nachher = XrModell.Stand(p).Geschosse.ToDictionary(g => g.Geschoss.Kennzeichen, g => g.SchluesselModell);
        Assert.NotEqual(vorher["EG"], nachher["EG"]);
        Assert.Equal(vorher["UG"], nachher["UG"]);
        Assert.Equal(vorher["1.OG"], nachher["1.OG"]);
        // auch die Datei zeigt den neuen Raum: Höhe 2.4 m statt 2.5 m
        var s = XrModell.Stand(p);
        var g = GlbPruefer.Pruefen(XrModell.Glb(s, "EG", XrModell.TeilModell));
        var id = p.Modell.Finde("E.01")!.Id;
        var (m, z) = Enumerable.Range(0, g.Meshes.Count).SelectMany(i => g.Zuordnung(i).Where(x => x.Id == id).Select(x => (i, x))).Single();
        Assert.Equal(2.4, g.Dreieckspunkte(m, z.Ab, z.Anzahl).Max(q => q[1]), 5);
        // Rückgängig: gleicher Inhalt, gleicher Schlüssel (der Schlüssel hängt am Inhalt, nicht am Änderungszähler)
        Ok(p.Rueckgaengig());
        var zurueck = XrModell.Stand(p).Geschosse.ToDictionary(x => x.Geschoss.Kennzeichen, x => x.SchluesselModell);
        Assert.Equal(vorher, zurueck);
    }

    [Fact]
    public void OhneGeschosse_EinErsatzgeschoss_LeereGueltigeGlb()
    {
        var p = Leer();
        var s = XrModell.Stand(p);
        var g = Assert.Single(s.Geschosse);
        Assert.Equal(XrModell.OhneGeschossId, g.Geschoss.Id);
        Assert.Equal(0, g.Elemente);
        var glb = GlbPruefer.Pruefen(XrModell.Glb(s, XrModell.OhneGeschossId, XrModell.TeilModell));
        Assert.Empty(glb.Meshes);
        Assert.Equal(0, (int)glb.SzeneExtras["netze"]!);
    }

    [Fact]
    public void Glb_UnbekanntesGeschossTeilOderOhneUnterlage_Fehler()
    {
        var s = XrModell.Stand(Beispiel);
        Assert.Throws<HlksFehler>(() => XrModell.Glb(s, "XX", XrModell.TeilModell));
        Assert.Throws<HlksFehler>(() => XrModell.Glb(s, "EG", "alles"));
        var f = Assert.Throws<HlksFehler>(() => XrModell.Glb(s, "EG", XrModell.TeilUnterlage));
        Assert.Contains("keine IFC-Unterlage", f.Message);
        Assert.NotNull(s.Geschoss("eg"));   // Kennzeichen ohne Gross-/Kleinschreibung
    }

    [Fact]
    public void Liste_GeschosseMitSchluesselnUndAdressen()
    {
        var s = XrModell.Stand(Beispiel);
        var l = XrModell.Liste(s, (id, teil) => $"/x/{id}/{teil}");
        Assert.Equal(XrModell.Fassung, (int)l["fassung"]!);
        var gs = (JsonArray)l["geschosse"]!;
        Assert.Equal(3, gs.Count);
        var eg = gs.Single(x => (string?)x!["kennzeichen"] == "EG")!;
        Assert.Equal(s.Geschoss("EG")!.SchluesselModell, (string?)eg["modell"]!["schluessel"]);
        Assert.Equal($"/x/{s.Geschoss("EG")!.Geschoss.Id}/modell", (string?)eg["modell"]!["url"]);
        Assert.Null(eg["unterlage"]);
        Assert.Equal(24, ((string)eg["modell"]!["schluessel"]!).Length);
    }

    // ------------------------------------------------------------------ IFC-Unterlage

    [Fact]
    public void Unterlage_JeGeschoss_NurHintergrund_ZuordnungNachGlobalId_InVrDicht()
    {
        var (p, _) = IfcArchitekturTests.Uebernommen("IFC4");
        var s = XrModell.Stand(p);
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var netz = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, ul.Text("netz")!));
        var hintergrund = netz.Produkte.Where(x => !x.Uebernommen && x.Dreiecke > 0).ToList();
        Assert.NotEmpty(hintergrund);
        var gesehen = new HashSet<string>();
        long dreiecke = 0;
        foreach (var g in s.Geschosse.Where(g => g.SchluesselUnterlage is not null))
        {
            var r = XrModell.Erzeugen(s, g.Geschoss.Id, XrModell.TeilUnterlage);
            var glb = GlbPruefer.Pruefen(r.Daten);
            var m = Assert.Single(glb.Meshes);
            var e = glb.Extras(0);
            Assert.Equal("unterlage", (string?)e["gruppe"]);
            Assert.Equal(ul.Id, (string?)e["unterlage"]);
            Assert.Equal("#8a8a8a", (string?)e["farbe"]);                 // GrundlagenIfc.Farbe (farbeOriginal fehlt)
            Assert.Equal(0.5, (double)e["deckkraft3d"]!);                  // Vorgabe der Unterlage
            Assert.Equal(1, (double)e["deckkraft"]!);                      // in VR undurchsichtig
            Assert.Equal("OPAQUE", (string?)glb.Material(0)["alphaMode"]);
            Assert.Equal("unterlage", (string?)glb.SzeneExtras["teil"]);
            Assert.Equal(g.SchluesselUnterlage, (string?)glb.SzeneExtras["schluessel"]);
            var z = glb.Zuordnung(0);
            Assert.Equal(g.Produkte, z.Count);
            foreach (var (gid, ab, anzahl, entitaet) in z)
            {
                var pr = netz.Produkt(gid)!;
                Assert.False(pr.Uebernommen);
                Assert.Equal(pr.Entitaet, entitaet);
                Assert.True(anzahl <= pr.Dreiecke);
                // Lage: alle Punkte des Bereichs in der Begrenzung des Produkts (KICAD-mm → glTF)
                foreach (var q in glb.Dreieckspunkte(0, ab, anzahl))
                {
                    var k = XrKoordinaten.Zurueck(q[0], q[1], q[2], s.Bezugspunkt);
                    for (int a = 0; a < 3; a++) Assert.InRange(k[a], pr.Begrenzung[a] - 0.5, pr.Begrenzung[a + 3] + 0.5);
                }
                Assert.True(gesehen.Add(gid), $"{gid} doppelt");
            }
            dreiecke += r.Dreiecke;
        }
        Assert.Equal(hintergrund.Select(x => x.GlobalId).Order(), gesehen.Order());
        Assert.Equal(hintergrund.Sum(x => (long)x.Dreiecke), dreiecke);   // die Testdatei hat keine entarteten Dreiecke
        // Objekt (Proxy) 1 × 1 × 2 m im EG: Volumen positiv (nach aussen gerichtet) und Geschoss EG
        var proxy = hintergrund.Single(x => x.Entitaet == "IfcBuildingElementProxy");
        var eg = s.Geschosse.Single(g => g.Geschoss.KoteMm == 0);
        var gl = GlbPruefer.Pruefen(XrModell.Glb(s, eg.Geschoss.Id, XrModell.TeilUnterlage));
        var b = gl.Zuordnung(0).Single(x => x.Id == proxy.GlobalId);
        Assert.Equal(2.0, gl.Volumen(0, b.Ab, b.Anzahl), 6);
        // Modell: die übernommenen Elemente stehen im Modell, nicht in der Unterlage
        var sued = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        var gm = GlbPruefer.Pruefen(XrModell.Glb(s, eg.Geschoss.Id, XrModell.TeilModell));
        Assert.Contains(Enumerable.Range(0, gm.Meshes.Count), m => gm.Zuordnung(m).Any(x => x.Id == sued.Id));
        Assert.DoesNotContain(sued.Kennungen!["ifc"], gesehen);
    }

    [Fact]
    public void Unterlage_FarbeGeaendert_NeuerSchluesselUnterlage_ModellBleibt_NachbaufarbeImModell()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var vorher = XrModell.Stand(p);
        var eg = vorher.Geschosse.Single(g => g.Geschoss.KoteMm == 0);
        Assert.NotNull(eg.SchluesselUnterlage);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ul.Kennzeichen, ["eigenschaften"] = new JsonObject { ["farbeOriginal"] = "#336699" } }));
        var nachher = XrModell.Stand(p);
        var eg2 = nachher.Geschoss(eg.Geschoss.Id)!;
        Assert.NotEqual(eg.SchluesselUnterlage, eg2.SchluesselUnterlage);
        Assert.Equal(eg.SchluesselModell, eg2.SchluesselModell);
        var glb = GlbPruefer.Pruefen(XrModell.Glb(nachher, eg.Geschoss.Id, XrModell.TeilUnterlage));
        Assert.Equal("#336699", (string?)glb.Extras(0)["farbe"]);

        // N12: farbeNachbau der Unterlage färbt die übernommenen Elemente (Wände) im Modell
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ul.Kennzeichen, ["eigenschaften"] = new JsonObject { ["farbeNachbau"] = "#ff0000" } }));
        var s3 = XrModell.Stand(p);
        Assert.NotEqual(eg2.SchluesselModell, s3.Geschoss(eg.Geschoss.Id)!.SchluesselModell);
        var gm = GlbPruefer.Pruefen(XrModell.Glb(s3, eg.Geschoss.Id, XrModell.TeilModell));
        var sued = IfcArchitekturTests.Namens(p, "Wand", "W-Sued");
        var rot = Enumerable.Range(0, gm.Meshes.Count).Where(m => (string?)gm.Extras(m)["farbe"] == "#ff0000").ToList();
        Assert.Contains(rot, m => (string?)gm.Extras(m)["gruppe"] == "architektur" && gm.Zuordnung(m).Any(x => x.Id == sued.Id));
        // alle Nachbau-Elemente rot, eigene Elemente nicht
        Assert.All(rot.SelectMany(gm.Zuordnung), x => Assert.Equal(ul.Id, p.Modell.Finde(x.Id)!.Kennungen?["quelle"]));
    }

    [Fact]
    public void Unterlage_Ausgeblendet_KeineUnterlage()
    {
        var p = Leer();
        Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = ul.Kennzeichen, ["eigenschaften"] = new JsonObject { ["anzeigen"] = false } }));
        Assert.All(XrModell.Stand(p).Geschosse, g => Assert.Null(g.SchluesselUnterlage));   // wie die 3D-Ansicht: ausgeblendet zeichnet nichts
    }
}
