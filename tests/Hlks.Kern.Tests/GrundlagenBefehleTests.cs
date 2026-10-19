using System.Text.Json.Nodes;
using ACadSharp.Types.Units;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Befehle unterlage_einfuegen und unterlage_kalibrieren (Modul 1b, B3): Lage, Umriss, Hinweise, Fehler, Rückgängig.</summary>
public class GrundlagenBefehleTests
{
    static double[] Ecke(Element u, int i) => u.Geometrie!.Punkte[i];

    // ---------------------------------------------------------------- Lage (Mathematik)

    [Fact]
    public void Lage_AufUndZurueck_ZweiPunkte()
    {
        var l = new UnterlagenLage(2, 90, 100, 200);
        var (x, y) = l.Auf(10, 0);
        Assert.Equal(100, x, 9); Assert.Equal(220, y, 9);
        var (u, v) = l.Zurueck(x, y);
        Assert.Equal(10, u, 9); Assert.Equal(0, v, 9);

        var z = UnterlagenLage.ZweiPunkte((1, 1), (4, 5), (10, 10), (10, 20));   // Länge 5 → 10, Richtung 53.13° → 90°
        Assert.Equal(2, z.Faktor, 9);
        Assert.Equal(90 - Math.Atan2(4, 3) * 180 / Math.PI, z.Drehung, 9);
        var (a, b) = z.Auf(1, 1); Assert.Equal(10, a, 9); Assert.Equal(10, b, 9);
        var (c, d) = z.Auf(4, 5); Assert.Equal(10, c, 9); Assert.Equal(20, d, 9);
        Assert.Throws<HlksFehler>(() => UnterlagenLage.ZweiPunkte((1, 1), (1, 1), (0, 0), (1, 1)));
        Assert.Throws<HlksFehler>(() => UnterlagenLage.ZweiPunkte((0, 0), (1, 1), (5, 5), (5, 5)));
    }

    // ---------------------------------------------------------------- einfügen

    [Fact]
    public void Einfuegen_Dxf_Meter_LegtUnterlageMitUmrissAn()
    {
        using var o = new Ordner();
        var p = Projekt();
        var datei = StandardDxf(o);
        var e = Ok(Einfuegen(p, datei));
        Assert.NotNull(e.PaketId);
        var ul = Unterlage(p);
        Assert.Equal("UL-1", ul.Kennzeichen);
        Assert.Equal("plan.dxf", ul.Name);
        Assert.Equal("Unterlage", ul.Klasse);
        Assert.Equal("dxf", ul.Text("art"));
        Assert.Equal(p.Modell.Hole("EG").Id, ul.Geschoss);
        Assert.Matches("^[0-9a-f]{12}_plan.dxf$", ul.Text("datei"));
        Assert.Equal(datei, ul.Text("original"));
        Assert.Equal(12, ul.Text("stand")!.Length);
        Assert.Equal(0.5, ul.Zahl("deckkraft"));
        Assert.True(ul.Eigenschaft("anzeigen")!.Inhalt!.GetValue<bool>());
        Assert.Equal("import", ul.Eigenschaft("datei")!.Herkunft.Art);
        Assert.Equal("unterlage_einfuegen", ul.Eigenschaft("datei")!.Herkunft.Befehl);
        Assert.True(File.Exists(Unterlagen.Pfad(p, ul.Text("datei")!)));                                  // Original abgelegt
        Assert.True(File.Exists(Unterlagen.Pfad(p, ul.Text("datei")!) + ".dwg.json"));                     // Ableitung

        // erste Lage: Meter → mm, Ursprung (0, 0)
        var t = ul.Eigenschaft("transformation")!.Inhalt!.AsObject();
        Assert.Equal(1000, t["faktor"]!.GetValue<double>());
        Assert.Equal(0, t["drehung"]!.GetValue<double>());
        Assert.Equal(0, t["dx"]!.GetValue<double>()); Assert.Equal(0, t["dy"]!.GetValue<double>());
        Assert.Equal(1000, t["erwartung"]!.GetValue<double>());
        // Umriss im Modell: Rechteck 10 × 6 m
        Assert.Equal("polygon", ul.Geometrie!.Art);
        Assert.Equal(4, ul.Geometrie.Punkte.Count);
        var box = Box(ul.Geometrie.Punkte);
        Nahe([0, 0, 10000, 6000], box);
        Assert.All(ul.Geometrie.Punkte, q => Assert.Equal(0, q[2]));   // Kote des EG

        Assert.Contains(e.Hinweise, h => h.Contains("UL-1") && h.Contains("Meters") && h.Contains("10.0 × 6.0 m"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Layer") && h.Contains("MOEBEL") && h.Contains("WAND"));
        Assert.Contains(e.Hinweise, h => h.Contains("unterlage_kalibrieren"));
    }

    [Fact]
    public void Einfuegen_ObergeschossBekommtKoteDesGeschosses()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o), geschoss: "1.OG"));
        Assert.All(Unterlage(p).Geometrie!.Punkte, q => Assert.Equal(3000, q[2]));
        Assert.Equal(p.Modell.Hole("1.OG").Id, Unterlage(p).Geschoss);
    }

    [Fact]
    public void Einfuegen_OhneEinheit_AnnahmeMillimeterMitHinweis()
    {
        using var o = new Ordner();
        var p = Projekt();
        var e = Ok(Einfuegen(p, StandardDxf(o, einheit: UnitsType.Unitless)));
        Assert.Equal(1, UnterlagenLage.Lesen(Unterlage(p)).Faktor);
        Assert.Contains(e.Hinweise, h => h.Contains("keine Zeichnungseinheit") && h.Contains("Annahme Millimeter"));
        Assert.Contains("Annahme", Unterlage(p).Eigenschaft("transformation")!.Inhalt!["grundlage"]!.GetValue<string>());
    }

    [Fact]
    public void Einfuegen_Trockenlauf_AendertNichts_Rueckgaengig_StelltHer()
    {
        using var o = new Ordner();
        var p = Projekt();
        var datei = StandardDxf(o);
        var vorher = p.Modell.Elemente.Count;
        var tl = Ok(Einfuegen(p, datei, trocken: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(vorher, p.Modell.Elemente.Count);
        Assert.Contains(tl.Aenderungen, a => a.Art == "angelegt" && a.Klasse == "Unterlage");

        Ok(Einfuegen(p, datei));
        Assert.Equal(vorher + 1, p.Modell.Elemente.Count);
        Ok(p.Rueckgaengig());
        Assert.Equal(vorher, p.Modell.Elemente.Count);
        Assert.Null(p.Modell.Finde("UL-1"));
    }

    [Fact]
    public void Einfuegen_DieselbeDateiNochmal_ZeigtUnveraendert_NeueFassungBehaeltDieKalibrierung()
    {
        using var o = new Ordner();
        var p = Projekt();
        var datei = StandardDxf(o);
        Ok(Einfuegen(p, datei));
        var nochmal = Ok(Einfuegen(p, datei));
        Assert.Null(nochmal.PaketId);
        Assert.Contains(nochmal.Hinweise, h => h.Contains("unverändert"));
        Assert.Single(p.Modell.DerKlasse("Unterlage"));

        // kalibrieren (drehen), dann neue Fassung derselben Datei (anderer Inhalt, gleicher Name)
        Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[0, 0], [0, 10000]]));
        var alt = Unterlage(p);
        var altStand = alt.Text("stand");
        using var o2 = new Ordner();
        var neu = Dxf(o2, "plan.dxf", UnitsType.Meters, doc =>
        {
            doc.Entities.Add(Linie(doc, 0, 0, 20, 0, "WAND"));
            doc.Entities.Add(Linie(doc, 20, 0, 20, 5, "WAND"));
        });
        var e = Ok(Einfuegen(p, neu));
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
        var ul = Unterlage(p);
        Assert.Equal(alt.Id, ul.Id);
        Assert.NotEqual(altStand, ul.Text("stand"));
        Assert.Equal(90, UnterlagenLage.Lesen(ul).Drehung, 6);                   // Kalibrierung bleibt
        Assert.Contains(e.Hinweise, h => h.Contains("neue Fassung"));
    }

    [Fact]
    public void Einfuegen_Fehler_haben_Vorschlag()
    {
        using var o = new Ordner();
        var p = Projekt();
        var datei = StandardDxf(o);
        void Nein(BefehlsErgebnis e, string teil) { Assert.False(e.Angenommen); Assert.Contains(teil, e.Fehler); Assert.False(string.IsNullOrWhiteSpace(e.Vorschlag)); }
        Nein(Einfuegen(p, o.Datei("gibtsnicht.dxf")), "gibt es nicht");
        Nein(Einfuegen(p, datei, "XX"), "XX");
        Nein(Einfuegen(p, datei, "EG", new JsonObject { ["art"] = "ifc" }), "IFC");
        Nein(Einfuegen(p, datei, "EG", new JsonObject { ["art"] = "tiff" }), "Art");
        var ohneEndung = o.Datei("daten.xyz"); File.WriteAllText(ohneEndung, "x");
        Nein(Einfuegen(p, ohneEndung), "Endung");
        var ifc = o.Datei("a.ifc"); File.WriteAllText(ifc, "x");
        var f = Einfuegen(p, ifc);
        Nein(f, "IFC"); Assert.Contains("ifc_uebernehmen", f.Vorschlag);
        var pdf = Pdf(o, "a.pdf");
        Nein(Einfuegen(p, pdf, "EG", new JsonObject { ["seite"] = 0 }), "Seitennummer");
        Nein(Einfuegen(p, pdf, "EG", new JsonObject { ["seite"] = 3 }), "1 Seite");
        Nein(Einfuegen(p, pdf, "EG", new JsonObject { ["dpi"] = 5 }), "dpi");
        Nein(Einfuegen(p, pdf, "EG", new JsonObject { ["massstab"] = 0 }), "massstab");
        var kaputt = o.Datei("kaputt.dwg"); File.WriteAllText(kaputt, "kein dwg");
        Nein(Einfuegen(p, kaputt), "lässt sich nicht lesen");
        var leer = Dxf(o, "leer.dxf", UnitsType.Meters, _ => { });
        Nein(Einfuegen(p, leer), "nichts");
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
    }

    [Fact]
    public void Einfuegen_MehrereUnterlagenBekommenFortlaufendeKennzeichen()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        Ok(Einfuegen(p, Png(o, "foto.png", 10, 10, (255, 0, 0)), "1.OG"));
        Assert.Equal(["UL-1", "UL-2"], p.Modell.DerKlasse("Unterlage").Select(u => u.Kennzeichen).Order());
    }

    // ---------------------------------------------------------------- kalibrieren

    [Fact]
    public void Kalibrieren_ZweiPunkte_SetztFaktorDrehungVerschiebung_UndFuehrtDenUmrissMit()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        // 10 Zeichnungseinheiten (Meter) entlang x → 10 m nach Norden, Ursprung nach (1000, 2000)
        var e = Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[1000, 2000], [1000, 12000]]));
        var l = UnterlagenLage.Lesen(Unterlage(p));
        Assert.Equal(1000, l.Faktor, 6); Assert.Equal(90, l.Drehung, 6); Assert.Equal(1000, l.Dx, 6); Assert.Equal(2000, l.Dy, 6);
        // Umriss: (0,0) → (1000,2000); (10,0) → (1000,12000); (10,6) → (−5000,12000); (0,6) → (−5000,2000)
        Nahe([1000, 2000], Ecke(Unterlage(p), 0)); Nahe([1000, 12000], Ecke(Unterlage(p), 1));
        Nahe([-5000, 12000], Ecke(Unterlage(p), 2)); Nahe([-5000, 2000], Ecke(Unterlage(p), 3));
        Assert.Contains(e.Hinweise, h => h.Contains("Faktor 1000") && h.Contains("Drehung 90°"));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("ACHTUNG"));
        Assert.Equal(1000, Unterlage(p).Eigenschaft("transformation")!.Inhalt!["erwartung"]!.GetValue<double>());   // Erwartung bleibt

        Ok(p.Rueckgaengig());
        Assert.Equal(0, UnterlagenLage.Lesen(Unterlage(p)).Drehung);
        Nahe([0, 0, 10000, 6000], Box(Unterlage(p).Geometrie!.Punkte));
    }

    [Fact]
    public void Kalibrieren_WarntBeiFaktorAbweichungUeberEinProzent()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        var innen = Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[0, 0], [10050, 0]]));   // 0.5 % Abweichung
        Assert.DoesNotContain(innen.Hinweise, h => h.Contains("ACHTUNG"));
        var aussen = Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[0, 0], [10500, 0]]));   // 5 %
        var w = Assert.Single(aussen.Hinweise, h => h.Contains("ACHTUNG"));
        Assert.Contains("5 %", w); Assert.Contains("1000", w); Assert.Contains("INSUNITS", w);
        Assert.Equal(1050, UnterlagenLage.Lesen(Unterlage(p)).Faktor, 6);   // gilt trotzdem
    }

    [Fact]
    public void Kalibrieren_NurVerschieben_BehaeltFaktorUndDrehung()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[0, 0], [0, 10000]]));    // 90°
        var e = Ok(Kalibrieren(p, "UL-1", [[0, 0]], [[500, 700]], nurVerschieben: true));
        var l = UnterlagenLage.Lesen(Unterlage(p));
        Assert.Equal(1000, l.Faktor, 6); Assert.Equal(90, l.Drehung, 6); Assert.Equal(500, l.Dx, 6); Assert.Equal(700, l.Dy, 6);
        Assert.Contains(e.Hinweise, h => h.Contains("verschoben"));
        // zwei Paare, die nicht zu einer Verschiebung passen → Warnung
        var w = Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[0, 0], [0, 12000]], nurVerschieben: true));
        Assert.Contains(w.Hinweise, h => h.Contains("ACHTUNG") && h.Contains("Verschiebung"));
    }

    [Fact]
    public void Kalibrieren_Fehler_haben_Vorschlag()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        void Nein(BefehlsErgebnis e, string teil) { Assert.False(e.Angenommen); Assert.Contains(teil, e.Fehler); Assert.False(string.IsNullOrWhiteSpace(e.Vorschlag)); }
        Nein(Kalibrieren(p, "UL-9", [[0, 0], [1, 0]], [[0, 0], [1, 0]]), "UL-9");
        Nein(Kalibrieren(p, "UL-1", [[1, 1], [1, 1]], [[0, 0], [1, 0]]), "gleich");
        Nein(Kalibrieren(p, "UL-1", [[0, 0], [1, 0]], [[5, 5], [5, 5]]), "gleich");
        Nein(Kalibrieren(p, "UL-1", [[0, 0]], [[0, 0]]), "genau zwei");
        Nein(Kalibrieren(p, "UL-1", [[0, 0], [1, 0], [2, 0]], [[0, 0], [1, 0], [2, 0]]), "genau zwei");
        Nein(Kalibrieren(p, "UL-1", [[0, 0], [1, 0]], [[0, 0]]), "Punkt");
        Nein(p.Ausfuehren("unterlage_kalibrieren", new JsonObject { ["unterlage"] = "UL-1", ["von"] = new JsonArray(1, 2), ["nach"] = new JsonArray(1, 2) }), "Punkt");
        // IFC: nicht hier
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Unterlage", ["kennzeichen"] = "UL-IFC",
            ["eigenschaften"] = new JsonObject { ["datei"] = "x.ifc", ["art"] = "ifc" },
            ["geometrie"] = new JsonObject { ["art"] = "polygon", ["punkte"] = Pts([0, 0], [1, 0], [1, 1]) },
        }));
        var f = Kalibrieren(p, "UL-IFC", [[0, 0], [1, 0]], [[0, 0], [1, 0]]);
        Nein(f, "IFC"); Assert.Contains("ifc_uebernehmen", f.Vorschlag);
        // Trockenlauf ändert nichts
        var vorher = UnterlagenLage.Lesen(Unterlage(p));
        Ok(Kalibrieren(p, "UL-1", [[0, 0], [10, 0]], [[0, 0], [0, 10000]], trocken: true));
        Assert.Equal(vorher, UnterlagenLage.Lesen(Unterlage(p)));
    }

    // ---------------------------------------------------------------- Plangrafik (DWG/DXF)

    [Fact]
    public void Plan_Dxf_ZeichnetGrauDuennAufEbeneGrundlage_MitDeckkraft_Layer_Ausblenden_Anzeigen()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        var g = PlanGrundlage(p);
        var teil = Assert.Single(g.Teile);
        Assert.Equal("grundlage", teil.Ebene);
        Assert.Equal(Unterlage(p).Id, teil.Id);
        Assert.Equal(4, teil.Primitive.Count(x => x.Art == "linie"));
        var kreis = Assert.Single(teil.Primitive, x => x.Art == "kreis");
        Assert.Equal(1000, kreis.Radius!.Value, 6);                       // 1 m
        Nahe([5000, 3000], kreis.Punkte[0]);
        Assert.All(teil.Primitive, x =>
        {
            Assert.Equal(Stifte.Duenn, x.Stil.Stift);
            Assert.Equal("#8a8a8a", x.Stil.Farbe);
            Assert.Equal(0.5, x.Stil.Deckkraft);
        });
        Nahe([0, 0, 10000, 6000], g.Begrenzung!);

        // Deckkraft der Unterlage
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "UL-1", ["eigenschaften"] = new JsonObject { ["deckkraft"] = 0.8 } }));
        Assert.All(Grundlage(PlanGrundlage(p)), x => Assert.Equal(0.8, x.Stil.Deckkraft));

        // Layer ausblenden
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "UL-1", ["eigenschaften"] = new JsonObject { ["ebenenAus"] = new JsonArray("moebel") } }));   // Gross-/Kleinschreibung egal
        Assert.DoesNotContain(Grundlage(PlanGrundlage(p)), x => x.Art == "kreis");
        Assert.Equal(4, Grundlage(PlanGrundlage(p)).Count);

        // ausblenden
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "UL-1", ["eigenschaften"] = new JsonObject { ["anzeigen"] = false } }));
        Assert.Empty(PlanGrundlage(p).Teile);
    }

    [Fact]
    public void Plan_Dxf_NurImGrundrissDesEigenenGeschosses()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o), geschoss: "1.OG"));
        Assert.Empty(PlanGrundlage(p, "AN-EG").Teile);
    }

    [Fact]
    public void Plan_KalibrierteUnterlage_DrehtTextUndBoegen_TextgroesseInPapierMm()
    {
        using var o = new Ordner();
        var p = Projekt();
        var datei = Dxf(o, "t.dxf", UnitsType.Millimeters, doc =>
        {
            doc.Entities.Add(new ACadSharp.Entities.TextEntity { Value = "Küche", InsertPoint = new CSMath.XYZ(1000, 1000, 0), Height = 100 });
            doc.Entities.Add(new ACadSharp.Entities.Arc { Center = new CSMath.XYZ(0, 0, 0), Radius = 500, StartAngle = 0, EndAngle = Math.PI / 2 });
            doc.Entities.Add(Linie(doc, 0, 0, 2000, 0));
        });
        Ok(Einfuegen(p, datei));
        var normal = Grundlage(PlanGrundlage(p));
        var text = Assert.Single(normal, x => x.Art == "text");
        Assert.Equal(2, text.Groesse!.Value, 9);        // 100 mm im Modell bei 1:50 = 2 mm Papier
        Assert.Null(text.Winkel);
        Assert.Equal("links", text.Ausrichtung);

        Ok(Kalibrieren(p, "UL-1", [[0, 0], [2000, 0]], [[0, 0], [0, 2000]]));   // 90° drehen, Faktor 1
        var gedreht = Grundlage(PlanGrundlage(p));
        var t2 = Assert.Single(gedreht, x => x.Art == "text");
        Assert.Equal(90, t2.Winkel!.Value, 6);
        Nahe([-1000, 1000], t2.Punkte[0]);
        var b = Assert.Single(gedreht, x => x.Art == "bogen");
        Assert.Equal(90, b.Von!.Value, 6); Assert.Equal(180, b.Bis!.Value, 6);
        Assert.Equal(500, b.Radius!.Value, 6);

        // Massstab 1:100: Text halb so gross auf dem Papier
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-EG", ["eigenschaften"] = new JsonObject { ["massstab"] = 100 } }));
        Assert.Equal(1, Assert.Single(Grundlage(PlanGrundlage(p)), x => x.Art == "text").Groesse!.Value, 9);
    }

    [Fact]
    public void Plan_FehlendeDatei_laesstDenGrundrissBestehen()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        var ul = Unterlage(p);
        var pfad = Unterlagen.Pfad(p, ul.Text("datei")!);
        File.Delete(pfad); File.Delete(pfad + ".dwg.json");
        Assert.Empty(PlanGrundlage(p).Teile.SelectMany(t => t.Primitive));   // keine Ausnahme
    }

    [Fact]   // B3-1 behoben am 17.10.2026 (Hauptlauf)
    public void Plan_UeberGrafikPlan_enthaeltDieUnterlage()
    {
        using var o = new Ordner();
        var p = Projekt();
        Ok(Einfuegen(p, StandardDxf(o)));
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal(5, Grundlage(g).Count);
    }
}
