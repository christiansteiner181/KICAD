using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>N12 – IFC: Original oder Nachbau als Unterlage zeigen, je eigene Farbe (transparent). Ansichtseigenschaft ifcDarstellung
/// (nachbau | original | beide), Farben an der Unterlage (farbeOriginal, deckkraft, farbeNachbau, deckkraftNachbau); Grundriss,
/// Schnitt, Ansicht und Körper. Datei: IfcArchitekturTestdatei (Wände 6000 × 4000 im EG, Tür, Fenster, Stütze, Unterzug; im Hintergrund
/// Rundwand und Objekt-Quader bei x 8000…9000).</summary>
public class IfcDarstellungTests
{
    static readonly object sperre = new();
    static Projekt? projekt;
    static int zaehler;

    static Projekt P()
    {
        lock (sperre)
        {
            if (projekt is not null) return projekt;
            var p = Leer();
            Ok(Uebernehmen(p, IfcArchitekturTestdatei.Datei("IFC4", false)));
            return projekt = p;
        }
    }

    static Element Unterlage(Projekt p) => p.Modell.DerKlasse("Unterlage").Single();
    static Element Wand(Projekt p, string name) => IfcArchitekturTests.Namens(p, "Wand", name);

    static void Setzen(Projekt p, Element e, JsonObject eig) => Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = e.Kennzeichen, ["eigenschaften"] = eig }));

    /// <summary>Grundriss EG (Schnitthöhe 1000) mit der gewünschten Darstellung; eigene Ansicht je Aufruf.</summary>
    static string Grundriss(Projekt p, string? ifc, string? darstellung = null)
    {
        var kz = "GR-N12-" + Interlocked.Increment(ref zaehler);
        var eig = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 };
        if (ifc is not null) eig["ifcDarstellung"] = ifc;
        if (darstellung is not null) eig["darstellung"] = darstellung;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["geschoss"] = "EG", ["eigenschaften"] = eig }));
        return kz;
    }

    /// <summary>Schnitt quer durch das Rechteck: Linie x = 3000 von Süd nach Nord, u = y + 1000.</summary>
    static string Schnitt(Projekt p, string? ifc, string art = "schnitt")
    {
        var kz = "S-N12-" + Interlocked.Increment(ref zaehler);
        var eig = new JsonObject { ["art"] = art, ["massstab"] = 50, ["blick"] = "links" };
        if (ifc is not null) eig["ifcDarstellung"] = ifc;
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = kz, ["eigenschaften"] = eig,
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = new JsonArray(new JsonArray(3000, -1000), new JsonArray(3000, 5000)) },
        }));
        return kz;
    }

    static double[] Box(IEnumerable<double[]> punkte) => ArchitekturHilfe.Box(punkte.ToList());

    static bool Hat(IEnumerable<Primitiv> ps, double x, double y, double tol = 1) => ps.Any(p => Hat(p.Punkte, x, y, tol));
    static bool Hat(IEnumerable<double[]> l, double x, double y, double tol = 1) => l.Any(q => Math.Abs(q[0] - x) <= tol && Math.Abs(q[1] - y) <= tol);

    static bool Nahe(double[] a, double[] b, double tol = 1) => a.Length == b.Length && a.Zip(b).All(z => Math.Abs(z.First - z.Second) <= tol);

    // ------------------------------------------------------------------ Modus

    [Fact]
    public void Modus_VorgabeNachbau_UnbekanntesUndAndereDarstellungenGeltenAlsNachbau()
    {
        var p = P();
        Assert.Equal("nachbau", IfcDarstellung.Modus(p.Modell.Finde(Grundriss(p, null))));
        Assert.Equal("original", IfcDarstellung.Modus(p.Modell.Finde(Grundriss(p, "original"))));
        Assert.Equal("beide", IfcDarstellung.Modus(p.Modell.Finde(Grundriss(p, "beide"))));
        // in der Hülle gilt die Einstellung nicht (eigene Farben)
        Assert.Equal("nachbau", IfcDarstellung.Modus(p.Modell.Finde(Grundriss(p, "original", "huelle"))));
        Assert.Equal("nachbau", IfcDarstellung.Modus(null));
    }

    [Fact]
    public void Katalog_NenntDieNeuenEigenschaften()
    {
        var p = P();
        var ansicht = p.Katalog.Klassen["Ansicht"].Eigenschaften.Single(e => e.Name == "ifcDarstellung");
        Assert.Equal(["nachbau", "original", "beide"], ansicht.Auswahl);
        var ul = p.Katalog.Klassen["Unterlage"].Eigenschaften.Select(e => e.Name).ToList();
        Assert.Contains("farbeOriginal", ul); Assert.Contains("farbeNachbau", ul); Assert.Contains("deckkraftNachbau", ul);
    }

    // ------------------------------------------------------------------ Grundriss

    [Fact]
    public void Grundriss_Nachbau_WieBisher_NachbauWaendeUndNurHintergrund()
    {
        var p = P();
        var g = Grafik.Plan(p, Grundriss(p, null));
        var sued = Wand(p, "W-Sued");
        Assert.Contains(g.Teile, t => t.Id == sued.Id);
        Assert.Contains(g.Teile, t => t.Id is null && t.Ebene == "wand");
        var netz = g.Teile.Single(t => t.Id == Unterlage(p).Id);
        // Hintergrund: Objekt-Quader, aber keine übernommene Wand (kein Linienzug in der Wandzeile y 0…200 bis x 6000)
        Assert.Contains(netz.Primitive, x => Nahe(Box(x.Punkte), [8000, 0, 9000, 1000], 0.5));
        Assert.False(Hat(netz.Primitive, 6000, 200) || Hat(netz.Primitive, 0, 200), "keine übernommene Wand im Hintergrund");
        Assert.All(netz.Primitive, x => Assert.Equal("#8a8a8a", x.Stil.Farbe));
    }

    [Fact]
    public void Grundriss_Original_ZeigtAlleProdukte_NachbauEntfaellt()
    {
        var p = P();
        var g = Grafik.Plan(p, Grundriss(p, "original"));
        // keine Nachbau-Elemente aus der Unterlage: weder Wand, Tür noch der gemeinsame Wandumriss
        foreach (var klasse in new[] { "Wand", "Tuer", "Fenster", "Stuetze", "Traeger" })
            foreach (var e in p.Modell.DerKlasse(klasse))
                Assert.DoesNotContain(g.Teile, t => t.Id == e.Id);
        Assert.DoesNotContain(g.Teile, t => t.Id is null && t.Ebene == "wand");
        var netz = g.Teile.Single(t => t.Id == Unterlage(p).Id);
        Assert.Contains(netz.Primitive, x => Nahe(Box(x.Punkte), [8000, 0, 9000, 1000], 0.5));   // Hintergrund bleibt
        Assert.True(Hat(netz.Primitive, 0, 0) && Hat(netz.Primitive, 6000, 0) && Hat(netz.Primitive, 0, 200) && Hat(netz.Primitive, 6000, 200), "Südwand des Originals");
        Assert.True(Hat(netz.Primitive, 5800, 4000) && Hat(netz.Primitive, 5800, 0), "Ostwand des Originals");
    }

    [Fact]
    public void Grundriss_Beide_NachbauUndOriginalUebereinander()
    {
        var p = P();
        var g = Grafik.Plan(p, Grundriss(p, "beide"));
        Assert.Contains(g.Teile, t => t.Id == Wand(p, "W-Sued").Id);
        Assert.Contains(g.Teile, t => t.Id is null && t.Ebene == "wand");
        var netz = g.Teile.Single(t => t.Id == Unterlage(p).Id);
        Assert.True(Hat(netz.Primitive, 0, 0) && Hat(netz.Primitive, 6000, 200));
        // Die Unterlage liegt unten (Ebene grundlage), der Nachbau darüber
        Assert.Equal("grundlage", netz.Ebene);
        Assert.True(Ebenen.Rang("grundlage") < Ebenen.Rang("wand"));
    }

    [Fact]
    public void Grundriss_Original_InDerHuelleBleibtDerNachbau()
    {
        var p = P();
        var g = Grafik.Plan(p, Grundriss(p, "original", "huelle"));
        Assert.Contains(g.Teile, t => t.Id == Wand(p, "W-Sued").Id || t.Id is null);
    }

    [Fact]
    public void Grundriss_AusgeblendeteUnterlage_ZeigtDenNachbauWieBisher()
    {
        var p = P();
        var ul = Unterlage(p);
        Setzen(p, ul, new JsonObject { ["anzeigen"] = false });
        try
        {
            var g = Grafik.Plan(p, Grundriss(p, "original"));
            Assert.Contains(g.Teile, t => t.Id == Wand(p, "W-Sued").Id);
            Assert.DoesNotContain(g.Teile, t => t.Id == ul.Id);
        }
        finally { Setzen(p, ul, new JsonObject { ["anzeigen"] = true }); }
    }

    [Fact]
    public void Grundriss_FarbeOriginalUndDeckkraft_ImStil()
    {
        var p = P();
        var ul = Unterlage(p);
        Setzen(p, ul, new JsonObject { ["farbeOriginal"] = "#1E64FF", ["deckkraft"] = 0.3 });
        try
        {
            var netz = Grafik.Plan(p, Grundriss(p, "beide")).Teile.Single(t => t.Id == ul.Id);
            Assert.All(netz.Primitive, x => { Assert.Equal("#1e64ff", x.Stil.Farbe); Assert.Equal(0.3, x.Stil.Deckkraft); });
        }
        finally { Setzen(p, ul, new JsonObject { ["farbeOriginal"] = null, ["deckkraft"] = null }); }
    }

    [Fact]
    public void Grundriss_UngueltigeFarbe_GiltAlsKeineAngabe()
    {
        var p = P();
        var ul = Unterlage(p);
        Setzen(p, ul, new JsonObject { ["farbeOriginal"] = "blau", ["farbeNachbau"] = "rot" });
        try
        {
            var g = Grafik.Plan(p, Grundriss(p, "beide"));
            Assert.All(g.Teile.Single(t => t.Id == ul.Id).Primitive, x => Assert.Equal("#8a8a8a", x.Stil.Farbe));
            Assert.All(g.Teile.Single(t => t.Id == Wand(p, "W-Sued").Id).Primitive, x => Assert.NotEqual("rot", x.Stil.Farbe));
        }
        finally { Setzen(p, ul, new JsonObject { ["farbeOriginal"] = null, ["farbeNachbau"] = null }); }
    }

    [Fact]
    public void Grundriss_FarbeNachbau_WandFlaechenUndGemeinsamerUmriss()
    {
        var p = P();
        var ul = Unterlage(p);
        var ohne = Grafik.Plan(p, Grundriss(p, "nachbau"));
        Assert.All(ohne.Teile.Single(t => t.Id == Wand(p, "W-Sued").Id).Primitive, x => Assert.NotEqual("#ff8000", x.Stil.Farbe));
        Setzen(p, ul, new JsonObject { ["farbeNachbau"] = "#ff8000", ["deckkraftNachbau"] = 0.4 });
        try
        {
            var g = Grafik.Plan(p, Grundriss(p, "beide"));
            var wand = g.Teile.Single(t => t.Id == Wand(p, "W-Sued").Id);
            Assert.All(wand.Primitive, x =>
            {
                Assert.Equal("#ff8000", x.Stil.Farbe);
                Assert.Equal("#ff8000", x.Stil.Fuellung);   // gefüllte Fläche in der Farbe, ohne Schraffur
                Assert.Null(x.Stil.Muster);
                Assert.Equal(0.4, x.Stil.Deckkraft);
            });
            // gemeinsamer Umriss der Wände in der Farbe des Nachbaus (Linie, dick), Deckkraft der Linie
            var umriss = g.Teile.Where(t => t.Id is null && t.Ebene == "wand").SelectMany(t => t.Primitive).ToList();
            Assert.NotEmpty(umriss);
            Assert.All(umriss, x => { Assert.Equal("#ff8000", x.Stil.Farbe); Assert.Equal(Stifte.Dick, x.Stil.Stift); Assert.Equal(0.4, x.Stil.Deckkraft); });
            // Original in der Vorgabefarbe daneben: beide lassen sich unterscheiden
            Assert.All(g.Teile.Single(t => t.Id == ul.Id).Primitive, x => Assert.Equal("#8a8a8a", x.Stil.Farbe));
        }
        finally { Setzen(p, ul, new JsonObject { ["farbeNachbau"] = null, ["deckkraftNachbau"] = null }); }
    }

    [Fact]
    public void Grundriss_NurDeckkraftNachbau_LaesstFarbenUnveraendert()
    {
        var p = P();
        var ul = Unterlage(p);
        var farbeVorher = Grafik.Plan(p, Grundriss(p, "nachbau")).Teile.Single(t => t.Id == Wand(p, "W-Sued").Id).Primitive[0].Stil;
        Setzen(p, ul, new JsonObject { ["deckkraftNachbau"] = 0.25 });
        try
        {
            var s = Grafik.Plan(p, Grundriss(p, "nachbau")).Teile.Single(t => t.Id == Wand(p, "W-Sued").Id).Primitive[0].Stil;
            Assert.Equal(0.25, s.Deckkraft);
            Assert.Equal(farbeVorher.Farbe, s.Farbe);
            Assert.Equal(farbeVorher.Fuellung, s.Fuellung);
            Assert.Equal(farbeVorher.Muster, s.Muster);
        }
        finally { Setzen(p, ul, new JsonObject { ["deckkraftNachbau"] = null }); }
    }

    [Fact]
    public void Grundriss_VarianteImSpeicher_NachbauUndOriginalVertauschenNichts()
    {
        var p = P();
        var a = Grafik.Plan(p, Grundriss(p, "original")).Teile.Single(t => t.Id == Unterlage(p).Id).Primitive.Count;
        var b = Grafik.Plan(p, Grundriss(p, "nachbau")).Teile.Single(t => t.Id == Unterlage(p).Id).Primitive.Count;
        var c = Grafik.Plan(p, Grundriss(p, "original")).Teile.Single(t => t.Id == Unterlage(p).Id).Primitive.Count;
        Assert.True(a > b);
        Assert.Equal(a, c);
    }

    // ------------------------------------------------------------------ Schnitt und Ansicht

    [Fact]
    public void Schnitt_Nachbau_ZeigtKeinOriginal()
    {
        var p = P();
        var g = Grafik.Plan(p, Schnitt(p, null));
        Assert.DoesNotContain(g.Teile, t => t.Id == Unterlage(p).Id);
        Assert.Contains(g.Teile, t => t.Id == Wand(p, "W-Sued").Id);
    }

    [Fact]
    public void Schnitt_Original_SchnittlinienDerNetzeUndKeinNachbau()
    {
        var p = P();
        var g = Grafik.Plan(p, Schnitt(p, "original"));
        Assert.DoesNotContain(g.Teile, t => t.Id == Wand(p, "W-Sued").Id || t.Id == Wand(p, "W-Nord").Id);
        var netz = g.Teile.Single(t => t.Id == Unterlage(p).Id);
        Assert.Equal("grundlage", netz.Ebene);
        Assert.Equal(g.Teile[0].Id, netz.Id);   // unterste Ebene zuerst
        Assert.All(netz.Primitive, x => { Assert.Equal("#8a8a8a", x.Stil.Farbe); Assert.Equal(Stifte.Duenn, x.Stil.Stift); });
        // Südwand (y 0…200 → u 1000…1200) und Nordwand (y 3800…4000 → u 4800…5000), je 2600 hoch
        var punkte = netz.Primitive.SelectMany(x => x.Punkte).ToList();
        Assert.Contains(punkte, q => Math.Abs(q[0] - 1000) < 1 && Math.Abs(q[1] - 2600) < 1);
        Assert.Contains(punkte, q => Math.Abs(q[0] - 1200) < 1 && Math.Abs(q[1]) < 1);
        Assert.Contains(punkte, q => Math.Abs(q[0] - 4800) < 1 && Math.Abs(q[1] - 2600) < 1);
        Assert.Contains(punkte, q => Math.Abs(q[0] - 5000) < 1 && Math.Abs(q[1]) < 1);
    }

    [Fact]
    public void Schnitt_Beide_NachbauUndOriginal_FarbenGetrennt()
    {
        var p = P();
        var ul = Unterlage(p);
        Setzen(p, ul, new JsonObject { ["farbeOriginal"] = "#0066ff", ["farbeNachbau"] = "#ff8000", ["deckkraftNachbau"] = 0.5 });
        try
        {
            var g = Grafik.Plan(p, Schnitt(p, "beide"));
            var wand = g.Teile.Single(t => t.Id == Wand(p, "W-Sued").Id);
            Assert.Contains(wand.Primitive, x => x.Art == "polygon" && x.Stil.Fuellung == "#ff8000" && x.Stil.Deckkraft == 0.5);
            Assert.All(g.Teile.Single(t => t.Id == ul.Id).Primitive, x => Assert.Equal("#0066ff", x.Stil.Farbe));
        }
        finally { Setzen(p, ul, new JsonObject { ["farbeOriginal"] = null, ["farbeNachbau"] = null, ["deckkraftNachbau"] = null }); }
    }

    [Fact]
    public void Ansicht_Original_ZeigtDieSchnittlinienInDerEbene()
    {
        var p = P();
        var g = Grafik.Plan(p, Schnitt(p, "original", "ansicht"));
        Assert.Equal("ansicht", g.Art);
        Assert.DoesNotContain(g.Teile, t => t.Id == Wand(p, "W-Sued").Id);
        Assert.NotEmpty(g.Teile.Single(t => t.Id == Unterlage(p).Id).Primitive);
    }

    [Fact]
    public void Schnitt_SenkrechteEbene_Schnittfunktion_GleichWieImGrundriss()
    {
        var p = P();
        var pfad = Unterlagen.Pfad(p, Unterlage(p).Text("netz")!);
        // Schnitt mit der Ebene x = 3000 (Linie von Süd nach Nord): u = y + 1000
        var raum = Schnittraum.Aus(p.Modell.Finde(Schnitt(p, null))!);
        var linien = GrundlagenIfc.SchnittSenkrecht(pfad, raum);
        Assert.NotEmpty(linien);
        Assert.Same(linien, GrundlagenIfc.SchnittSenkrecht(pfad, raum));   // zwischengespeichert
        // waagrecht (Grundriss) geschnitten liegt die Südwand bei y 0…200; senkrecht bei u 1000…1200 – gleiche Breite
        var wagrecht = GrundlagenIfc.Schnitt(pfad, 1000, nurHintergrund: false);
        Assert.Contains(wagrecht, l => Hat(l, 0, 0) && Hat(l, 6000, 200));
        Assert.Contains(linien, l => l.Any(q => Math.Abs(q[0] - 1000) < 1) && l.Any(q => Math.Abs(q[0] - 1200) < 1));
    }

    // ------------------------------------------------------------------ Körper (3D)

    [Fact]
    public void Koerper_NetzTeil_TraegtFarbeUndDeckkraftDesOriginals()
    {
        var p = P();
        var ul = Unterlage(p);
        var vorgabe = Grafik.Koerper(p, [ul.Id]).Single().Teile.Single();
        Assert.Equal("netz", vorgabe.Art);
        Assert.Equal("#8a8a8a", vorgabe.Farbe);
        Assert.Equal(0.5, vorgabe.Deckkraft);
        Setzen(p, ul, new JsonObject { ["farbeOriginal"] = "#0066ff", ["deckkraft"] = 0.35 });
        try
        {
            var t = Grafik.Koerper(p, [ul.Id]).Single().Teile.Single();
            Assert.Equal("#0066ff", t.Farbe);
            Assert.Equal(0.35, t.Deckkraft);
        }
        finally { Setzen(p, ul, new JsonObject { ["farbeOriginal"] = null, ["deckkraft"] = null }); }
    }
}
