using System.Reflection;
using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using M = Hlks.Kern.Tests.AussparungMaterialTestdatei;
using S = Hlks.Kern.Tests.NetzdateiTestschreiber;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag B3c (Entscheid Christian 19.10.2026, «Aussparungen nur in Beton und Mauerwerk»): Wortliste deutsch/englisch/
/// französisch, Einteilung eines Bauteils (Rohbau, kein Rohbau, unbekannt), Material in der Netzdatei Fassung 3 (IFC4 und IFC2X3,
/// am Xbim-Schema geprüft; Fassungen 1 und 2 bleiben lesbar) und Material der KICAD-Bauteile (material, Aufbau, aufbau). Alle
/// Materialnamen FIKTIV – es geht nur um Namen, keine Kennwerte.</summary>
public sealed class AussparungMaterialTests : IDisposable
{
    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    // ------------------------------------------------------------------ Wortliste

    [Theory]
    // deutsch
    [InlineData("Stahlbeton", Materialgruppe.Beton)]
    [InlineData("Beton Sichtbeton, hellgrau", Materialgruppe.Beton)]
    [InlineData("Kalksandstein", Materialgruppe.Mauerwerk)]
    [InlineData("Backstein 150", Materialgruppe.Mauerwerk)]
    [InlineData("Porenbeton", Materialgruppe.Mauerwerk)]
    [InlineData("Hochlochziegel", Materialgruppe.Mauerwerk)]
    [InlineData("Wärmedämmung", Materialgruppe.Daemmung)]
    [InlineData("Daemmung EPS", Materialgruppe.Daemmung)]
    [InlineData("Mineralwolle", Materialgruppe.Daemmung)]
    [InlineData("Steinwolle", Materialgruppe.Daemmung)]
    [InlineData("Gipskarton", Materialgruppe.Leichtbau)]
    [InlineData("Metallständer CW 50", Materialgruppe.Leichtbau)]
    [InlineData("Brettschichtholz", Materialgruppe.Holz)]
    [InlineData("Holz Schalung grau stehend", Materialgruppe.Holz)]
    [InlineData("Holzfaserdämmung", Materialgruppe.Daemmung)]
    [InlineData("Stahl S235", Materialgruppe.Metall)]
    [InlineData("Metallfenster", Materialgruppe.Metall)]
    [InlineData("Universal H-Träger 220 x 220", Materialgruppe.Metall)]
    [InlineData("Kalkzementputz", Materialgruppe.Ausbau)]
    [InlineData("Zementestrich", Materialgruppe.Bodenaufbau)]                         // Nachtrag B3e: eigene Gruppe
    [InlineData("Parkett Ahorn", Materialgruppe.Ausbau)]
    [InlineData("Dachziegel", Materialgruppe.Ausbau)]
    [InlineData("Glas Rauchglas", Materialgruppe.Ausbau)]
    [InlineData("DirectShape:abgehängte Decke 26 cm 260", Materialgruppe.Unterdecke)] // Unterdecke wie Bekleidung (Befund Bernstrasse, Typname; Nachtrag B3e)
    [InlineData("Unterdecke", Materialgruppe.Unterdecke)]
    [InlineData("Faux plafond", Materialgruppe.Unterdecke)]
    [InlineData("Suspended ceiling", Materialgruppe.Unterdecke)]
    // englisch
    [InlineData("Concrete, Cast-in-Place", Materialgruppe.Beton)]
    [InlineData("Brick, Common", Materialgruppe.Mauerwerk)]
    [InlineData("Masonry - Concrete Block", Materialgruppe.Mauerwerk)]
    [InlineData("Rigid insulation", Materialgruppe.Daemmung)]
    [InlineData("Gypsum Wall Board", Materialgruppe.Leichtbau)]
    [InlineData("Wood - Timber", Materialgruppe.Holz)]
    [InlineData("Structural Steel", Materialgruppe.Metall)]
    [InlineData("Plaster", Materialgruppe.Ausbau)]
    // französisch
    [InlineData("Béton armé", Materialgruppe.Beton)]
    [InlineData("Maçonnerie de briques", Materialgruppe.Mauerwerk)]
    [InlineData("Brique silico-calcaire", Materialgruppe.Mauerwerk)]
    [InlineData("Laine de verre", Materialgruppe.Daemmung)]
    [InlineData("Plaque de plâtre", Materialgruppe.Leichtbau)]
    [InlineData("Bois lamellé-collé", Materialgruppe.Holz)]
    [InlineData("Acier", Materialgruppe.Metall)]
    [InlineData("Enduit", Materialgruppe.Ausbau)]
    // ohne Aussage (Darstellungsmaterialien, Revit)
    [InlineData("Farbe, weiss", Materialgruppe.OhneAussage)]
    [InlineData("<Unnamed>", Materialgruppe.OhneAussage)]
    [InlineData("RAL 9010", Materialgruppe.OhneAussage)]
    [InlineData("Siemens_Grau", Materialgruppe.OhneAussage)]
    [InlineData("Luftschicht", Materialgruppe.Luftschicht)]                           // Nachtrag B3e: eigene Gruppe
    [InlineData("Luftspalt", Materialgruppe.OhneAussage)]                             // übrige Namen mit «Luft» wie bisher
    public void Wortliste_DeutschEnglischFranzoesisch(string name, Materialgruppe erwartet) =>
        Assert.Equal(erwartet, Materialwortliste.Gruppe(name));

    [Theory]
    [InlineData("Material 42")]
    [InlineData("Rahmen")]
    [InlineData("2.3_IW 1.5/8/1.5 cm 110")]
    [InlineData("DirectShape:5.1.1_Geschossdecke UB ohne Dämmung 105")]   // «ohne Dämmung» ist keine Dämmung (Befund Bernstrasse)
    public void Wortliste_Unbekannt_IstNull(string name) => Assert.Null(Materialwortliste.Gruppe(name));

    [Fact]
    public void Wortliste_EineStelle_AnnahmeMarkiert_RohbauNurBetonUndMauerwerk()
    {
        Assert.Contains("ANNAHME", Materialwortliste.Annahme);
        Assert.Equal(Enum.GetValues<Materialgruppe>(), Materialwortliste.Woerter.Select(w => w.Gruppe).ToArray());   // jede Gruppe genau einmal, in der Prüffolge
        Assert.Equal([Materialgruppe.Mauerwerk, Materialgruppe.Beton], Enum.GetValues<Materialgruppe>().Where(Materialwortliste.Rohbau));
        Assert.Equal("ae ss a e c", Materialwortliste.Normalisieren(" Ae  ß ä é_ç "));
    }

    // ------------------------------------------------------------------ Einteilung

    static Materialangabe A(string text, bool typ = false) => new(text, "Test", typ);

    [Fact]
    public void Einteilung_RohbauSchlaegtAlles_UnbekanntWennNichtAllesErkannt_TypnameNurWennErkannt()
    {
        // Beton neben Dämmung und Putz: Rohbau (ein Bauteil ist ein Prüfkörper)
        var b = Materialbefund.Einteilen([A("Kalkputz"), A("Stahlbeton"), A("Mineralwolle")]);
        Assert.True(b.Rohbau); Assert.False(b.Unbekannt); Assert.True(b.Aussparung);
        Assert.Equal("Beton: Stahlbeton", b.Text);
        Assert.Equal(Materialgruppe.Beton, b.Gruppe);
        // alles erkannt, kein Rohbau
        var g = Materialbefund.Einteilen([A("Gipskarton"), A("Mineralwolle")]);
        Assert.True(g.KeinRohbau);
        Assert.Equal("Leichtbau: Gipskarton | Dämmung: Mineralwolle", g.Text);
        Assert.Equal(Materialgruppe.Leichtbau, g.Gruppe);
        // ein unbekannter Name neben erkannten: unbekannt (lieber ein Vorschlag zu viel)
        var u = Materialbefund.Einteilen([A("Gipskarton"), A("Material 42")]);
        Assert.True(u.Unbekannt); Assert.True(u.Aussparung);
        Assert.Equal("unbekannt: Gipskarton | Material 42", u.Text);
        // nur Darstellungsmaterialien: unbekannt
        Assert.True(Materialbefund.Einteilen([A("Farbe, weiss"), A("<Unnamed>")]).Unbekannt);
        // keine Angabe: unbekannt mit Hinweis
        var leer = Materialbefund.Einteilen([], "Netzdatei Fassung 2 ohne Material");
        Assert.True(leer.Unbekannt);
        Assert.Equal("unbekannt: Netzdatei Fassung 2 ohne Material", leer.Text);
        Assert.Contains("ANNAHME", leer.Quelle);
        // Typname: zählt nur erkannt – «Beton, Stahlbeton 250» macht Rohbau, ein unbekannter Typname sagt nichts
        var typ = Materialbefund.Einteilen([A("Farbe, weiss"), A("Beton, Stahlbeton 250", typ: true)]);
        Assert.True(typ.Rohbau);
        Assert.Equal("Beton: aus Typname «Beton, Stahlbeton 250»", typ.Text);              // Herkunft sichtbar (Nachtrag B3d)
        Assert.Equal(Materialherkunft.Typname, typ.Herkunft);
        var holz = Materialbefund.Einteilen([A("<Unnamed>"), A("1.0_AW Holz 2.5/6/6/20 cm 445", typ: true)]);
        Assert.True(holz.KeinRohbau);
        var nichts = Materialbefund.Einteilen([A("Holz Schalung"), A("2.3_IW 1.5/8/1.5 cm 110", typ: true)]);
        Assert.True(nichts.KeinRohbau);                                       // unbekannter Typname macht nichts unbekannt
        Assert.Single(nichts.Angaben);
    }

    // ------------------------------------------------------------------ Xbim-Schema

    /// <summary>IfcRelAssociatesMaterial.RelatingMaterial (IfcMaterialSelect) am Xbim-Schema: IFC2X3 kennt IfcMaterial, IfcMaterialList,
    /// IfcMaterialLayerSetUsage, IfcMaterialLayerSet, IfcMaterialLayer; IFC4 zusätzlich Profil- und Konstituenten-Mengen. Jede Klasse setzt
    /// die Schnittstelle um, die IfcNetzMaterial liest. Bekleidung: IfcCovering in beiden Fassungen.</summary>
    [Fact]
    public void MaterialSelect_AmXbimSchema()
    {
        var ifc4 = typeof(Xbim.Ifc4.MaterialResource.IfcMaterial).Assembly;
        var ifc2x3 = typeof(Xbim.Ifc2x3.MaterialResource.IfcMaterial).Assembly;
        Type? T(Assembly a, string name) => a.GetTypes().SingleOrDefault(t => t.Name == name && t.Namespace?.EndsWith("MaterialResource") == true);
        foreach (var (name, schnittstelle) in new[]
                 {
                     ("IfcMaterial", typeof(IIfcMaterial)), ("IfcMaterialList", typeof(IIfcMaterialList)), ("IfcMaterialLayerSetUsage", typeof(IIfcMaterialLayerSetUsage)),
                     ("IfcMaterialLayerSet", typeof(IIfcMaterialLayerSet)), ("IfcMaterialLayer", typeof(IIfcMaterialLayer)),
                 })
        {
            Assert.True(schnittstelle.IsAssignableFrom(T(ifc4, name)), $"IFC4 {name}");
            Assert.True(schnittstelle.IsAssignableFrom(T(ifc2x3, name)), $"IFC2X3 {name}");
            Assert.True(typeof(IIfcMaterialSelect).IsAssignableFrom(T(ifc2x3, name)), $"IFC2X3 {name} ist IfcMaterialSelect");
        }
        foreach (var (name, schnittstelle) in new[]
                 {
                     ("IfcMaterialConstituentSet", typeof(IIfcMaterialConstituentSet)), ("IfcMaterialProfileSet", typeof(IIfcMaterialProfileSet)),
                     ("IfcMaterialProfileSetUsage", typeof(IIfcMaterialProfileSetUsage)),
                 })
        {
            Assert.True(schnittstelle.IsAssignableFrom(T(ifc4, name)), $"IFC4 {name}");
            Assert.Null(T(ifc2x3, name));                                    // gibt es in IFC2X3 nicht
        }
        Assert.NotNull(typeof(Xbim.Ifc4.MaterialResource.IfcMaterial).GetProperty("Category"));   // IFC4: Category
        // Bekleidung
        Assert.Contains(ifc4.GetTypes(), t => t.Name == "IfcCovering" && typeof(IIfcCovering).IsAssignableFrom(t));
        Assert.Contains(ifc2x3.GetTypes(), t => t.Name == "IfcCovering" && typeof(IIfcCovering).IsAssignableFrom(t));
        Assert.DoesNotContain(ifc4.GetTypes(), t => t.Name.StartsWith("IfcCovering") && t.Name != "IfcCovering" && t.Name != "IfcCoveringType"
                                                    && typeof(IIfcProduct).IsAssignableFrom(t));
        Assert.Equal(["IfcCovering"], Pruefkoerperbau.BekleidungEntitaeten);
    }

    // ------------------------------------------------------------------ Netzdatei Fassung 3

    Projekt Uebernommen(string schema)
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, M.Datei(schema), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));   // alles Hintergrund
        return p;
    }

    static IfcNetzdatei Netz(Projekt p) => IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, p.Modell.DerKlasse("Unterlage").Single().Text("netz")!));

    [Theory]
    [InlineData("IFC4")]
    [InlineData("IFC2X3")]
    public void Netzdatei_Fassung3_MaterialUndTypname_AusExemplarTypListeSchichtenKonstituenten(string schema)
    {
        var p = Uebernommen(schema);
        var n = Netz(p);
        Assert.Equal(3, n.Fassung);
        Assert.Contains("Materialnamen", n.Beschreibung);
        IfcNetzProdukt P(string name) => n.Produkt(M.Gid(name)) ?? throw new Xunit.Sdk.XunitException($"{name} fehlt");
        // IfcMaterial (IFC4 mit Category in Klammern)
        Assert.Equal([schema == "IFC4" ? "Stahlbeton (Concrete)" : "Stahlbeton"], P("W-MAT").Materialien);
        Assert.StartsWith("IfcMaterial #", P("W-MAT").Materialquelle);
        Assert.Equal(["Kalksandstein", "Kalkputz"], P("W-LIST").Materialien);
        Assert.StartsWith("IfcMaterialList #", P("W-LIST").Materialquelle);
        Assert.Equal(["Gipskarton", "Mineralwolle"], P("W-LSU").Materialien);
        Assert.StartsWith("IfcMaterialLayerSetUsage #", P("W-LSU").Materialquelle);
        // am Typobjekt
        Assert.Equal(["Backstein"], P("W-TYP").Materialien);
        Assert.Contains(" am Typ #", P("W-TYP").Materialquelle);
        Assert.Equal("Typ Backstein", P("W-TYP").Typname);
        // ObjectType als Typname (Revit), Darstellungsmaterial
        Assert.Equal(["<Unnamed>"], P("W-OBJ").Materialien);
        Assert.Equal("Beton, Stahlbeton 250", P("W-OBJ").Typname);
        Assert.Empty(P("W-OHNE").Materialien);
        Assert.Null(P("W-OHNE").Materialquelle);
        Assert.Equal("IfcCovering", P("B-BEKL").Entitaet);
        Assert.Equal(["Gipsputz"], P("B-BEKL").Materialien);
        if (schema == "IFC4")
        {
            Assert.Equal(["Brettschichtholz", "Holzfaserdämmung"], P("W-KONST").Materialien);
            Assert.StartsWith("IfcMaterialConstituentSet #", P("W-KONST").Materialquelle);
        }
        // Prüfkörper tragen es weiter; Einteilung
        var menge = Pruefkoerperbau.Erzeugen(p);
        Materialbefund B(string name) => Bauteilmaterial.Von(p.Modell, p.Katalog, menge.Koerper.Single(k => k.GlobalId == M.Gid(name)));
        Assert.True(B("W-MAT").Rohbau);
        Assert.True(B("W-LIST").Rohbau);
        Assert.True(B("W-LSU").KeinRohbau);
        Assert.True(B("W-TYP").Rohbau);
        Assert.True(B("W-OBJ").Rohbau);                                       // über den Typnamen
        Assert.True(B("W-OHNE").Unbekannt);
        Assert.Equal("unbekannt: keine Materialzuordnung im IFC", B("W-OHNE").Text);
        var bek = menge.Koerper.Single(k => k.GlobalId == M.Gid("B-BEKL"));
        Assert.True(bek.Bekleidung);
        Assert.True(Bauteilmaterial.Von(p.Modell, p.Katalog, bek).KeinRohbau);
        Assert.False(Aussparungsregeln.Aussparungsfaehig(p.Katalog, p.Modell, bek));
        if (schema == "IFC4") Assert.True(B("W-KONST").KeinRohbau);
    }

    [Fact]
    public void Netzdatei_Fassung1Und2_BleibenLesbar_OhneMaterial_UnbekanntMitHinweis()
    {
        var p = Uebernommen("IFC4");
        var saetze = new[] { S.Quader("3WANDALT", "IfcWall", [0, 0, 0], [1000, 200, 2600], materialien: ["Stahlbeton"], typname: "Typ") };
        foreach (var fassung in new[] { 1, 2, 3 })
        {
            var name = S.AlsNetzDerUnterlage(p, fassung, saetze);
            var n = IfcNetzdatei.Oeffnen(Unterlagen.Pfad(p, name));
            Assert.Equal(fassung, n.Fassung);
            var w = Assert.Single(n.Produkte);
            Assert.Equal(fassung >= 3 ? ["Stahlbeton"] : [], w.Materialien);
            Assert.Equal(fassung >= 3 ? "Typ" : null, w.Typname);
            var k = Pruefkoerperbau.Erzeugen(p).Koerper.Single(x => x.GlobalId == w.GlobalId);
            var b = Bauteilmaterial.Von(p.Modell, p.Katalog, k);
            if (fassung >= 3) Assert.True(b.Rohbau);
            else
            {
                Assert.True(b.Unbekannt);
                Assert.Equal($"unbekannt: Netzdatei Fassung {fassung} ohne Material – die IFC-Datei erneut übernehmen (Fassung 3)", b.Text);
            }
        }
    }

    // ------------------------------------------------------------------ KICAD-Bauteile (Modul 1c)

    [Fact]
    public void KicadBauteil_MaterialAufbauKurzbeschreibung()
    {
        var p = Rechteck("beton");
        projekte.Add(p);
        var menge = Pruefkoerperbau.Erzeugen(p);
        Materialbefund B(string kz) => Bauteilmaterial.Von(p.Modell, p.Katalog, menge.Koerper.Single(k => k.Kennzeichen == kz));
        Assert.True(B("W-1").Rohbau);
        Assert.Equal("Beton: beton", B("W-1").Text);
        Assert.Contains("W-1.material", B("W-1").Quelle);
        foreach (var (mat, rohbau) in new[] { ("leichtbau", false), ("holz", false), ("kalksandstein", true), ("porenbeton", true), ("unbestimmt", false) })
        {
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-2", ["eigenschaften"] = new JsonObject { ["material"] = mat } }));
            var b = Bauteilmaterial.Von(p.Modell, p.Katalog, Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Kennzeichen == "W-2"));
            Assert.Equal(rohbau, b.Rohbau);
            if (mat == "unbestimmt") Assert.True(b.Unbekannt);                // «unbestimmt» zählt nicht
            else if (!rohbau) Assert.True(b.KeinRohbau, mat);
        }
        // Aufbau mit Material-Elementen (Name und kategorie), ohne material
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-3", ["eigenschaften"] = new JsonObject { ["material"] = null } }));
        Ok(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "Gipskarton 12.5", ["kategorie"] = "Leichtbau" }));
        Ok(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "MW 50", ["kategorie"] = "Dämmstoff" }));
        Ok(p.Ausfuehren("material_anlegen", new JsonObject { ["name"] = "Tragschicht", ["kategorie"] = "Mauerwerk" }));
        Ok(HuelleTestHilfe.Aufbau(p, "Ständerwand", "wand", HuelleTestHilfe.Schichten(("MAT-1", 12.5), ("MAT-2", 50), ("MAT-1", 12.5))));
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-3"), ["aufbau"] = "AUF-1" }));
        var w3 = Bauteilmaterial.Von(p.Modell, p.Katalog, Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Kennzeichen == "W-3"));
        Assert.True(w3.KeinRohbau);
        Assert.Equal("Leichtbau: Gipskarton 12.5 (Leichtbau) | Dämmung: MW 50 (Dämmstoff)", w3.Text);
        Assert.Contains("Aufbau AUF-1, Schicht 2: MAT-2", w3.Quelle);
        Ok(HuelleTestHilfe.Aufbau(p, "Mauerwerk verputzt", "wand", HuelleTestHilfe.Schichten(("MAT-1", 12.5), ("MAT-3", 150))));
        Ok(p.Ausfuehren("aufbau_zuweisen", new JsonObject { ["elemente"] = new JsonArray("W-3"), ["aufbau"] = "AUF-2" }));
        Assert.True(Bauteilmaterial.Von(p.Modell, p.Katalog, Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Kennzeichen == "W-3")).Rohbau);
        // Kurzbeschreibung aufbau (wie aus IFC: «Name Dicke / …»)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-4", ["eigenschaften"] = new JsonObject { ["material"] = null, ["aufbau"] = "Kalkputz 15 / Backstein 150 / Steinwolle 120" } }));
        var w4 = Bauteilmaterial.Von(p.Modell, p.Katalog, Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Kennzeichen == "W-4"));
        Assert.True(w4.Rohbau);
        Assert.Equal("Mauerwerk: Backstein 150", w4.Text);
        // KICAD-Klassen sind keine Bekleidungen (keine Klasse hat IfcCovering)
        Assert.DoesNotContain(p.Katalog.Klassen.Values, d => Pruefkoerperbau.BekleidungEntitaeten.Contains(d.Ifc ?? ""));
        Assert.False(Pruefkoerperbau.IstBekleidung(p.Katalog, Hole(p, "W-1")));
        Assert.True(Pruefkoerperbau.IstBekleidung("IfcCovering"));
        Assert.False(Pruefkoerperbau.IstBekleidung("IfcWall"));
    }
}
