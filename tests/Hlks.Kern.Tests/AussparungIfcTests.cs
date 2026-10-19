using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.AussparungHilfe;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using N = Hlks.Kern.Tests.IfcNetzFassung2Testdatei;
using X = Hlks.Kern.Tests.AussparungIfcTestdatei;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B3 mit IFC-Unterlagen: Durchgänge in Architektenöffnungen (Testdatei von B1b) brauchen keine Aussparung;
/// Aussparungen in nicht übernommenen Produkten (Bezugsformat Unterlage + GlobalId); Abgleich mit einer neuen Architekten-Fassung
/// (angekommen über eine Öffnung bzw. ein ausgeschnittenes Netz, fehlt, nachgeholt). Werte FIKTIV (siehe AussparungHilfe).</summary>
public sealed class AussparungIfcTests : IDisposable
{
    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    Projekt Mit(string datei, params string[] bereiche)
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, datei, bereiche.Length == 0 ? null : new JsonObject { ["bereiche"] = new JsonArray(bereiche.Select(b => (JsonNode)b).ToArray()) }));
        Serie(p);
        Vorgaben(p);
        return p;
    }

    static Element MitGid(Projekt p, string gid) => p.Modell.Elemente.Single(e => e.Kennungen?.GetValueOrDefault("ifc") == gid);

    /// <summary>Rohr der Testserie quer durch eine Wand bei y 0 … 200 (y −500 … 700).</summary>
    static void Rohr(Projekt p, string kz, double x, double z, string typ = "DN 20") => Ok(Modul3Hilfe.Rohr(p, kz, typ, [x, -500, z], [x, 700, z]));

    [Fact]
    public void Architektenoeffnung_Uebernommen_KeinVorschlag_SonstVorschlag()
    {
        var p = Mit(N.Datei("IFC4"));
        var wa = MitGid(p, N.Gid("W-A"));
        Assert.Equal("Wand", wa.Klasse);
        Rohr(p, "RO-1", 1300, 1300);              // in O-Leer (x 1000 … 1600, z 1000 … 1600): keine Aussparung
        Rohr(p, "RO-2", 2500, 1300);              // massive Wand
        Rohr(p, "RO-3", 1010, 1300, "DN 50");     // Achse in O-Leer, Querschnitt x 982.5 … 1037.5 ragt hinaus
        var e = Ok(Vorschlagen(p));
        Assert.DoesNotContain(Aussparungen(p), a => LeitungKz(p, a).Contains("RO-1"));
        Assert.Contains(e.Hinweise, h => h.Contains($"RO-1 durch {wa.Kennzeichen}: liegt in der Öffnung {N.Gid("O-Leer")} der Architektur – keine Aussparung"));
        Assert.Contains(e.Hinweise, h => h.Contains("1 Durchgang/Durchgänge in Architektenöffnungen"));
        var a2 = VonLeitung(p, "RO-2");
        Assert.Equal(wa.Id, a2.Bezuege["bauteil"]);
        Etwa(70, a2.Zahl("b")); Etwa(70, a2.Zahl("h"));   // B3b: rechteckig, DN 20 (da 25) + 2 × 20 = 65 → ganze cm (B4e)
        Etwa(200, a2.Zahl("tiefe"));
        Assert.Equal([2500, 100, 1300], Mitte(a2));
        Assert.Equal(p.Modell.DerKlasse("Geschoss").Single(g => g.Zahl("kote") == 0).Id, a2.Geschoss);
        VonLeitung(p, "RO-3");
        Assert.Contains(e.Hinweise, h => h.Contains($"RO-3 durch {wa.Kennzeichen}: Achse in der Öffnung {N.Gid("O-Leer")}, der Querschnitt passt aber nicht hinein"));
    }

    /// <summary>Nachtrag B3b: aussparung_form auch, wenn das Bauteil ein Hintergrund-Produkt der IFC-Unterlage (Netzdatei) bzw. ein
    /// übernommenes IFC-Bauteil mit exaktem Netz ist. DN 20 (da 25, FIKTIV) senkrecht durch W-A (Dicke 200): automatisch 65 × 65 → ganze cm
    /// 70 × 70 (Nachtrag B4e), von Hand rund Ø 25 + 2 × 20 = 65 → 70; der Bezug bleibt.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Form_RundUndZurueck_BauteilAusDerIfcUnterlage(bool hintergrund)
    {
        var p = hintergrund ? Mit(N.Datei("IFC4"), "raeume") : Mit(N.Datei("IFC4"));
        Rohr(p, "RO-2", 2500, 1300);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-2");
        Assert.Equal(hintergrund, !a.Bezuege.ContainsKey("bauteil"));
        var schluessel = Aussparungsbezug.BauteilSchluessel(a);
        Etwa(70, a.Zahl("b")); Etwa(70, a.Zahl("h"));
        Ok(Form(p, "rund", a.Kennzeichen));
        var r = VonLeitung(p, "RO-2");
        Assert.Equal(("rund", "kernbohrung"), (r.Text("form"), r.Text("art")));
        Etwa(70, r.Zahl("d"));
        Assert.Equal(schluessel, Aussparungsbezug.BauteilSchluessel(r));
        Assert.Equal(Mitte(a), Mitte(r));
        // nachführen behält die Wahl
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);
        Ok(Form(p, "rechteckig", a.Kennzeichen));
        Etwa(70, VonLeitung(p, "RO-2").Zahl("b"));
    }

    [Fact]
    public void Hintergrund_BezugUnterlageUndGlobalId_Liste_P20()
    {
        var p = Mit(N.Datei("IFC4"), "raeume");   // Architektur nicht übernommen: W-A ist Hintergrund
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Rohr(p, "RO-1", 1300, 1300);   // in O-Leer
        Rohr(p, "RO-2", 2500, 1300);
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal(["RO-2"], LeitungKz(p, a));
        Assert.False(a.Bezuege.ContainsKey("bauteil"));
        Assert.Equal(ul.Id, a.Bezuege["unterlage"]);
        Assert.Equal(N.Gid("W-A"), a.Kennungen!["bauteil"]);
        Assert.Equal("IfcWall", a.Kennungen["bauteilEntitaet"]);
        Assert.Equal($"{ul.Id}:{N.Gid("W-A")}", Aussparungsbezug.BauteilSchluessel(a));
        Assert.Equal(N.Gid("W-A"), Aussparungsbezug.BauteilGlobalId(p.Modell, a));
        Assert.Equal(p.Modell.DerKlasse("Geschoss").Single(g => g.Zahl("kote") == 0).Id, a.Geschoss);   // nach der Kote des Produkts
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Element == a.Kennzeichen);   // kein P08: Bezug auf die Unterlage
        var z = Assert.Single(((JsonObject)Tuer.Abfrage(p, "aussparungsliste"))["zeilen"]!.AsArray())!.AsObject();
        Assert.Equal(N.Gid("W-A"), z["bauteil"]!.GetValue<string>());
        Assert.Equal("IfcWall", z["bauteilArt"]!.GetValue<string>());
        Assert.Equal(ul.Kennzeichen, z["unterlage"]!.GetValue<string>());
        // Auswahl über «Unterlage:GlobalId» bzw. die GlobalId
        Assert.Empty(Ok(Vorschlagen(p, new JsonObject { ["bauteile"] = new JsonArray($"{ul.Kennzeichen}:{N.Gid("W-A")}") })).Aenderungen);
        Assert.Empty(Ok(Vorschlagen(p, new JsonObject { ["bauteile"] = new JsonArray(N.Gid("W-A")) })).Aenderungen);
        // P20 mit Bauteil «Unterlage-Id:GlobalId» (wie Pruefkoerper.Kennung): abgedeckt; für RO-1 (in der Öffnung) ohne Aussparung gemeldet
        foreach (var (kz, ro) in new[] { ("KOL-1", "RO-2"), ("KOL-2", "RO-1") })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Kollision", ["kennzeichen"] = kz,
                ["eigenschaften"] = new JsonObject
                {
                    ["art"] = "hart", ["typ"] = "durchdringung", ["bauteil"] = $"{ul.Id}:{N.Gid("W-A")}",
                    ["beteiligte"] = new JsonArray(ro, new JsonObject { ["globalId"] = N.Gid("W-A"), ["unterlage"] = ul.Id }),
                },
                ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = ArchitekturHilfe.Pts([2500, 100, 1300]) },
            }));
        var b = Assert.Single(Modul3Hilfe.Befunde(p, "P20"));
        Assert.Equal("RO-1", b.Element);
        Assert.Contains($"durchdringt {N.Gid("W-A")} ({ul.Kennzeichen})", b.Text);
    }

    [Fact]
    public void Abgleich_NeueFassung_Angekommen_Fehlt_Nachgeholt()
    {
        var p = Mit(X.Datei("ohne"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        var wand = MitGid(p, X.Gid("W-X")); var decke = MitGid(p, X.Gid("D-X"));
        Rohr(p, "RO-1", 1500, 1200, "DN 50");   // Ø 95 bei x 1500
        Rohr(p, "RO-2", 4500, 1200, "DN 50");   // Ø 95 bei x 4500
        Kanal(p, "KA-1", 400, 200, [3000, 2000, 2500], [3000, 2000, 3500]);   // 440 × 240 in der Decke
        Ok(Vorschlagen(p));
        var a1 = VonLeitung(p, "RO-1").Kennzeichen; var a2 = VonLeitung(p, "RO-2").Kennzeichen; var ak = VonLeitung(p, "KA-1").Kennzeichen;
        Assert.Equal(wand.Id, Hole(p, a1).Bezuege["bauteil"]);
        Assert.Equal(decke.Id, Hole(p, ak).Bezuege["bauteil"]);
        // noch nichts versandt: nichts abzugleichen
        var leer = Ok(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = ul.Kennzeichen }));
        Assert.Empty(leer.Aenderungen);
        Ok(p.Ausfuehren("aussparungen_nummerieren"));
        Ok(Status(p, "freigegeben", a1, a2, ak));
        Ok(Status(p, "versandt", a1, a2, ak));

        // neue Fassung mit Öffnungen (gleicher Dateiname = dieselbe Unterlage)
        Ok(IfcHilfe.Uebernehmen(p, X.Datei("mit")));
        Assert.Single(p.Modell.DerKlasse("Unterlage"));
        var e = Ok(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = ul.Kennzeichen }));
        Assert.Equal("angekommen", Hole(p, a1).Text("status"));   // Öffnung O-X1 deckt sie ab
        Assert.Equal("fehlt", Hole(p, a2).Text("status"));        // O-X2 zu klein
        Assert.Equal("angekommen", Hole(p, ak).Text("status"));   // Decke an der Stelle ausgeschnitten
        Assert.Contains($"Öffnung {X.Gid("O-X1")}", Hole(p, a1).Eigenschaft("status")!.Herkunft.Quelle);
        Assert.Contains("ist an der Stelle offen", Hole(p, ak).Eigenschaft("status")!.Herkunft.Quelle);
        Assert.Contains("deckt sie ab", Hole(p, a2).Eigenschaft("status")!.Herkunft.Quelle);
        Assert.Contains(e.Hinweise, h => h.StartsWith($"Abgleich mit {ul.Kennzeichen}") && h.Contains("2 angekommen, 1 fehlt"));
        Assert.Contains(e.NeueBefunde, b => b.Code == "P19" && b.Element == a2);

        // nächste Fassung bringt die zweite Öffnung: fehlt → angekommen; angekommene bleiben
        Ok(IfcHilfe.Uebernehmen(p, X.Datei("nachgeholt")));
        var n = Ok(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = ul.Kennzeichen }));
        Assert.Equal("angekommen", Hole(p, a2).Text("status"));
        Assert.Equal(["angekommen", "angekommen", "angekommen"], new[] { a1, a2, ak }.Select(x => Hole(p, x).Text("status")));
        Assert.Contains(n.BehobeneBefunde, b => b.Code == "P19" && b.Element == a2);
        Assert.Single(n.Aenderungen);
    }

    [Fact]
    public void BauteilFehltInDerNeuenFassung_Fehlt_DannVeraltet_BezugEntfernt()
    {
        var p = Mit(X.Datei("ohne"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Rohr(p, "RO-1", 1500, 1200, "DN 50");
        Rohr(p, "RO-2", 4500, 1200, "DN 50");
        Ok(Vorschlagen(p));
        var a1 = VonLeitung(p, "RO-1").Kennzeichen; var a2 = VonLeitung(p, "RO-2").Kennzeichen;
        Ok(Status(p, "freigegeben", a1)); Ok(Status(p, "versandt", a1));
        // neue Fassung ohne die Wand: ifc_uebernehmen löscht das Element, die Bezüge der Aussparungen zeigen ins Leere (P08)
        // (ohne die Wand liegt der Inhalt neben dem bestehenden Modell – die Ausrichtung ausdrücklich wählen)
        Ok(IfcHilfe.Uebernehmen(p, X.Datei("ohnewand"), new JsonObject { ["ausrichtung"] = "projekt" }));
        Assert.DoesNotContain(p.Modell.Elemente, e => e.Kennungen?.GetValueOrDefault("ifc") == X.Gid("W-X"));
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P08" && b.Element == a1);
        Ok(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = ul.Kennzeichen }));
        Assert.Equal("fehlt", Hole(p, a1).Text("status"));
        Assert.Contains("Bauteil fehlt in der neuen Fassung", Hole(p, a1).Eigenschaft("status")!.Herkunft.Quelle);
        Assert.Equal("vorgeschlagen", Hole(p, a2).Text("status"));   // nicht abgegeben: kein Abgleich
        var e = Ok(Vorschlagen(p));
        foreach (var a in new[] { a1, a2 })
        {
            Assert.Equal("veraltet", Hole(p, a).Text("status"));
            Assert.Contains("Bauteil gelöscht", Hole(p, a).Eigenschaft("status")!.Herkunft.Quelle);
            Assert.False(Hole(p, a).Bezuege.ContainsKey("bauteil"));
        }
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P08");
        Assert.Contains(e.Hinweise, h => h.Contains($"{a1}: Bezug auf gelöschtes Bauteil"));
    }

    [Fact]
    public void Abgleich_OhneOeffnung_Fehlt_UndFalscheUnterlage()
    {
        var p = Mit(X.Datei("ohne"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Rohr(p, "RO-1", 1500, 1200, "DN 50");
        Ok(Vorschlagen(p));
        Ok(Status(p, "freigegeben", "AUS-1"));
        // dieselbe Fassung noch einmal: keine Öffnung, Wand massiv → fehlt (auch freigegeben wird geprüft)
        Ok(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = ul.Id }));
        Assert.Equal("fehlt", Hole(p, "AUS-1").Text("status"));
        Assert.Contains("keine Öffnung in", Hole(p, "AUS-1").Eigenschaft("status")!.Herkunft.Quelle);
        // Fehler: keine Unterlage bzw. keine IFC-Unterlage
        Assert.Contains("gibt es nicht", ArchitekturHilfe.Nein(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = "UL-99" })).Fehler);
        Assert.Contains("keine IFC-Unterlage", ArchitekturHilfe.Nein(p.Ausfuehren("aussparungen_abgleichen", new JsonObject { ["unterlage"] = "RO-1" })).Fehler);
    }

    [Fact]
    public void Abgleich_Pruefungen_AbdeckungUndFrei_Einheit()
    {
        // Öffnung 300 × 300 (x 1350 … 1650, z 1050 … 1350), Wand y 0 … 200
        var oe = new Pruefoeffnung
        {
            UnterlageId = "u", GlobalId = "o", Entitaet = "IfcOpeningElement",
            Netz = Pruefnetz.Aus(NetzBau.Quader([1350, -100, 1050], [1650, 300, 1350])),
        };
        var (u, v) = Durchgaenge.Ebenenachsen(new V3(0, 1, 0));
        Aussparungslage Rund(double x, double d) => new(new V3(x, 100, 1200), new V3(0, 1, 0), u, v, true, d, d, 200);
        Assert.True(AussparungenAbgleichen.Abgedeckt(oe, Rund(1500, 95)));
        Assert.True(AussparungenAbgleichen.Abgedeckt(oe, Rund(1500, 300)));    // genau so gross (Rand 0.1 mm)
        Assert.False(AussparungenAbgleichen.Abgedeckt(oe, Rund(1500, 301)));
        Assert.False(AussparungenAbgleichen.Abgedeckt(oe, Rund(1620, 95)));
        // Netz frei: massiver Quader nicht, Quader mit Loch ja
        var massiv = Pruefnetz.Aus(NetzBau.Quader([0, 0, 0], [6000, 200, 2600]));
        Assert.False(AussparungenAbgleichen.Frei(massiv, Rund(1500, 95)));
        var mitLoch = Pruefnetz.Aus(NetzBau.Extrusion("xy", [(0, 0), (6000, 0), (6000, 4000), (0, 4000)], [[(1000, 1000), (1300, 1000), (1300, 1300), (1000, 1300)]], 2750, 3000));
        var (du, dv) = Durchgaenge.Ebenenachsen(new V3(0, 0, 1));
        Assert.True(AussparungenAbgleichen.Frei(mitLoch, new Aussparungslage(new V3(1150, 1150, 2875), new V3(0, 0, 1), du, dv, false, 300, 300, 250)));
        Assert.False(AussparungenAbgleichen.Frei(mitLoch, new Aussparungslage(new V3(1150, 1150, 2875), new V3(0, 0, 1), du, dv, false, 320, 300, 250)));
        // dickeres Bauteil als die Aussparung (Mitte im Netz): nicht frei
        Assert.False(AussparungenAbgleichen.Frei(massiv, new Aussparungslage(new V3(1500, 100, 1200), new V3(0, 1, 0), u, v, true, 95, 95, 20)));
    }
}
