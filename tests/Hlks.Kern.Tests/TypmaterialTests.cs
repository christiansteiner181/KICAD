using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;
using S = Hlks.Kern.Tests.NetzdateiTestschreiber;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag B3d (Entscheide Christian 19.10.2026 «Material», entwurf/modul-3b.md): Material aus dem Typnamen (Herkunft «aus
/// Typname»), Typenliste «Typname → Material» je Projekt (Klasse Typmaterial, Befehl typmaterial_setzen, Abfrage typmaterial), Vorrang
/// Typenliste > Materialzuordnung im IFC > Typname-Wortliste > unbekannt, Kategorie installationsvorwand (keine Aussparung, Kollision nur
/// im Paar hlks-vorwand), bekleidung (Paar hlks-bekleidung). Hintergrund-Wände als Quader in einer Netzdatei Fassung 3 von Hand (entlang
/// x, Dicke in y), Rohre der Testserie DN 50 (da 55, FIKTIV) entlang y auf z = 1000; Vorgaben FIKTIV (Spiel 20, Zusammenfassen 100 mm).
/// Alle Material- und Typnamen FIKTIV – ausser den zwei echten Typnamen aus Christians Entscheid («2.3_IW 1.5/8/1.5 cm 110» = Gips/
/// Leichtbau, «_IW GIS 1.8/3.2 cm 50» = Sanitärvorwand Geberit GIS), hier als Beispiele mit der Revit-Familie «DirectShape:» davor wie in
/// der Bernstrasse 42.</summary>
public sealed class TypmaterialTests : IDisposable
{
    const string T110 = "2.3_IW 1.5/8/1.5 cm 110";
    const string TGis = "_IW GIS 1.8/3.2 cm 50";
    const string Christian = "Entscheid Christian 19.10.2026 (entwurf/modul-3b.md)";
    const string Fiktiv = "FIKTIV – Test B3d";

    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    Projekt Hintergrund(params S.Satz[] saetze)
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, IfcNetzFassung2Testdatei.Datei("IFC4"), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        S.AlsNetzDerUnterlage(p, 3, saetze);
        Serie(p); Vorgaben(p);
        return p;
    }

    static S.Satz Wand(string gid, double x0, double dicke, string typname, params string[] material) =>
        S.Quader(gid, "IfcWall", [x0, 0, 0], [x0 + 2000, dicke, 2600], materialien: material, typname: typname);

    static string G(string gid) => gid.PadRight(22, '0')[..22];

    static void RohrY(Projekt p, string kz, double x) => Ok(Modul3Hilfe.Rohr(p, kz, "DN 50", [x, -500, 1000], [x, 1000, 1000]));

    static JsonObject Typ(string typname, string? kategorie, string? quelle = null)
    {
        var o = new JsonObject { ["typname"] = typname, ["kategorie"] = kategorie };
        if (quelle is not null) o["quelle"] = quelle;
        return o;
    }

    static BefehlsErgebnis Setzen(Projekt p, bool trocken, string? quelle, params JsonObject[] typen)
    {
        var par = new JsonObject { ["typen"] = new JsonArray(typen.Select(x => (JsonNode)x).ToArray()) };
        if (quelle is not null) par["quelle"] = quelle;
        return p.Ausfuehren("typmaterial_setzen", par, trocken);
    }

    static JsonObject Abfrage(Projekt p, bool betroffen = false) =>
        (JsonObject)Tuer.Abfrage(p, "typmaterial", new JsonObject { ["betroffen"] = betroffen });

    static JsonNode Zeile(JsonObject o, string liste, string schluessel) =>
        o[liste]!.AsArray().SingleOrDefault(z => z!["schluessel"]!.GetValue<string>() == schluessel)
        ?? throw new Xunit.Sdk.XunitException($"{liste}: «{schluessel}» fehlt – {o[liste]!.ToJsonString()}");

    static List<Element> Kollisionen(Projekt p) => KollisionspruefungHilfe.Kollisionen(p);

    // ------------------------------------------------------------------ Vorrang der Quellen

    [Fact]
    public void Vorrang_TypenlisteVorMaterialzuordnungVorTypnameVorUnbekannt()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(Setzen(p, false, Fiktiv, Typ(T110, "leichtbau", Christian), Typ("FamA:Typ X", "beton")));
        var liste = Typenliste.Lesen(p.Modell);
        Assert.Equal(2, liste.Eintraege.Count);
        Materialbefund B(string? typ, params string[] mat) =>
            Bauteilmaterial.Von(p.Modell, p.Katalog, new Bauteilangaben(null, mat, typ, null, 3, false, "IfcWall"), liste);

        // 1. Typenliste schlägt die Materialzuordnung (auch Beton) – Festlegung mit Quelle, keine Annahme
        var a = B("DirectShape:" + T110, "Stahlbeton");
        Assert.True(a.KeinRohbau);
        Assert.Equal(Materialherkunft.Typenliste, a.Herkunft);
        Assert.Equal($"Leichtbau: Typenliste «DirectShape:{T110}»", a.Text);
        Assert.Equal($"Typenliste TM-1: «{T110}» → leichtbau (Quelle: {Christian})", a.Quelle);
        Assert.DoesNotContain("ANNAHME", a.Quelle);
        // 2. aussagekräftige Materialzuordnung (Bauart) schlägt den Typnamen
        var b = B("Beton, Stahlbeton 250", "Brettschichtholz");
        Assert.True(b.KeinRohbau);
        Assert.Equal((Materialgruppe?)Materialgruppe.Holz, b.Gruppe);
        Assert.Equal(Materialherkunft.Material, b.Herkunft);
        Assert.Equal("Holz: Brettschichtholz", b.Text);
        // 3. nur Darstellungsmaterial: der Typname gilt (Herkunft «aus Typname», Entscheid und Wortliste in der Quelle)
        var c = B("Beton, Stahlbeton 250", "Farbe, weiss");
        Assert.True(c.Rohbau);
        Assert.Equal(Materialherkunft.Typname, c.Herkunft);
        Assert.Equal("Beton: aus Typname «Beton, Stahlbeton 250»", c.Text);
        Assert.Contains(Materialwortliste.EntscheidTypname, c.Quelle);
        Assert.Contains("ANNAHME", c.Quelle);
        // ANNAHME «aussagekräftig»: Oberfläche allein (Putz) nennt keine Bauart – das verputzte Mauerwerk bleibt Rohbau
        var d = B("MW, Kalksandstein 150", "Kalkputz");
        Assert.True(d.Rohbau);
        Assert.Equal("Mauerwerk: aus Typname «MW, Kalksandstein 150»", d.Text);
        // 4. unbekannt: nichts erkennbar bzw. ein unbekannter Name neben einem Typnamen ohne Rohbau (lieber ein Vorschlag zu viel)
        var e = B("FIKTIV Typ 7", "<Unnamed>");
        Assert.True(e.Unbekannt);
        Assert.Equal(Materialherkunft.Keine, e.Herkunft);
        Assert.Equal("unbekannt: <Unnamed>", e.Text);
        Assert.True(B("Holz Typ FIKTIV", "Material 42").Unbekannt);
        Assert.Equal("unbekannt: keine Materialzuordnung im IFC", B(null).Text);
        // Bekleidung nach Entität (IfcCovering) bleibt Bekleidung, auch wenn ihr Typ in der Liste steht
        var bek = Bauteilmaterial.Von(p.Modell, p.Katalog, new Bauteilangaben(null, [], T110, null, 3, true, "IfcCovering"), liste);
        Assert.Equal(Materialherkunft.Entitaet, bek.Herkunft);
        Assert.True(bek.AlsBekleidung);
    }

    [Fact]
    public void Typname_GenauOderOhneFamilie_OhneGrossKleinUndLeerschlag_FamilieImEintragNurGenau()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(Setzen(p, false, Fiktiv, Typ(T110, "leichtbau"), Typ("FamA:Typ X", "beton")));
        var liste = Typenliste.Lesen(p.Modell);
        Assert.Equal("TM-1", liste.Finde(T110)?.Kennzeichen);
        Assert.Equal("TM-1", liste.Finde("DirectShape:" + T110)?.Kennzeichen);          // Revit «Familie:Typ»
        Assert.Equal("TM-1", liste.Finde("directshape:2.3_iw  1.5/8/1.5 CM 110 ")?.Kennzeichen);
        Assert.Equal("TM-2", liste.Finde("FamA:Typ X")?.Kennzeichen);
        Assert.Null(liste.Finde("FamB:Typ X"));                                          // eine andere Familie
        Assert.Null(liste.Finde("Typ X"));
        Assert.Null(liste.Finde("2.3_IW 1.5/8/1.5 cm 1100"));
        Assert.Equal(TGis, Typenliste.Kern("DirectShape:" + TGis));
        Assert.Equal("Beton, Stahlbeton 250", Typenliste.Kern("Beton, Stahlbeton 250"));
        Assert.Same(Typenliste.Von(p.Modell), Typenliste.Von(p.Modell));                  // zwischengespeichert, solange sich nichts ändert
        var vorher = Typenliste.Von(p.Modell);
        Ok(Setzen(p, false, Fiktiv, Typ("Typ Y", "holz")));
        Assert.NotSame(vorher, Typenliste.Von(p.Modell));
        Assert.Equal(3, Typenliste.Von(p.Modell).Eintraege.Count);
    }

    // ------------------------------------------------------------------ Wortliste: Installationsvorwand (ANNAHME)

    [Theory]
    [InlineData(TGis)]
    [InlineData("DirectShape:" + TGis)]
    [InlineData("Vorwand Sanitär 25")]
    [InlineData("Installationsvorwand WC")]
    [InlineData("Installationswand 20")]
    public void Wortliste_GisVorwandInstallationswand_Installationsvorwand(string typname) =>
        Assert.Equal(Materialgruppe.Installationsvorwand, Materialwortliste.Gruppe(typname));

    [Fact]
    public void Wortliste_Projekttyp110_NichtInDerAllgemeinenListe_VorwandAusTypnameMitAnnahme()
    {
        Assert.Null(Materialwortliste.Gruppe(T110));                                     // projektbezogen: nur über die Typenliste
        Assert.Null(Materialwortliste.Gruppe("DirectShape:" + T110));
        Assert.Equal(Materialgruppe.Leichtbau, Materialwortliste.Gruppe("Gipskarton"));
        Assert.Equal(Materialgruppe.Installationsvorwand, Materialwortliste.Woerter[0].Gruppe);   // vor den Baustoffen geprüft
        // Nachtrag B3e: die Wörter hat Christian am 19.10.2026 bestätigt – Entscheid statt ANNAHME
        Assert.StartsWith("Entscheid Christian 19.10.2026", Materialwortliste.EntscheidVorwand);
        Assert.DoesNotContain("ANNAHME", Materialwortliste.EntscheidVorwand);
        var p = IfcHilfe.Leer();
        var vw = Bauteilmaterial.Von(p.Modell, p.Katalog, new Bauteilangaben(null, ["<Unnamed>"], "DirectShape:" + TGis, null, 3, false, "IfcWall"));
        Assert.True(vw.Installationsvorwand);
        Assert.True(vw.KeinRohbau);
        Assert.Equal(Materialherkunft.Typname, vw.Herkunft);
        Assert.Equal($"Installationsvorwand: aus Typname «DirectShape:{TGis}»", vw.Text);
        Assert.Contains(Materialwortliste.EntscheidVorwand, vw.Quelle);
        // eine aussagekräftige Materialzuordnung geht vor (Leichtbau) – eine solche Vorwand legt die Typenliste fest
        var gk = Bauteilmaterial.Von(p.Modell, p.Katalog, new Bauteilangaben(null, ["Gipskarton"], "DirectShape:" + TGis, null, 3, false, "IfcWall"));
        Assert.False(gk.Installationsvorwand);
        Assert.Equal((Materialgruppe?)Materialgruppe.Leichtbau, gk.Gruppe);
    }

    // ------------------------------------------------------------------ typmaterial_setzen

    [Fact]
    public void Setzen_Trockenlauf_Setzen_Aendern_Unveraendert_Entfernen_Rueckgaengig()
    {
        var p = Hintergrund(Wand("3W110", 0, 110, "DirectShape:" + T110, "Farbe, weiss"), Wand("3WGIS", 3000, 50, "DirectShape:" + TGis, "<Unnamed>"));
        RohrY(p, "RO-1", 1000);
        Ok(p.Ausfuehren("kollision_pruefen"));   // zuerst: mit Aussparung wäre der Durchgang keine Kollision
        Ok(Vorschlagen(p));
        // Trockenlauf: nichts gespeichert, Hinweise mit den betroffenen Bauteilen
        var t = Ok(Setzen(p, true, Christian, Typ(T110, "leichtbau"), Typ(TGis, "installationsvorwand")));
        Assert.True(t.Trockenlauf);
        Assert.Equal(2, t.Aenderungen.Count);
        Assert.Empty(p.Modell.DerKlasse("Typmaterial"));
        Assert.Contains(t.Hinweise, h => h.StartsWith($"TM-1: «{T110}» → leichtbau (Quelle: {Christian}; neu) – keine Aussparung"));
        Assert.Contains(t.Hinweise, h => h.StartsWith($"TM-2: «{TGis}» → installationsvorwand") && h.Contains("Kollision nur im Paar hlks-vorwand"));
        Assert.Contains(t.Hinweise, h => h == $"«{T110}» (leichtbau): 1 Bauteil(e), 1 Aussparung(en), 1 Durchgang/Durchgänge («DirectShape:{T110}»).");
        Assert.Contains(t.Hinweise, h => h.StartsWith($"«{TGis}» (installationsvorwand): 1 Bauteil(e), 0 Aussparung(en), 0 Durchgang/Durchgänge"));
        Assert.Contains(t.Hinweise, h => h.StartsWith("Wirkung mit aussparungen_vorschlagen"));
        // setzen: je Typ ein Element mit Herkunft (Quelle) an typname und kategorie
        var e = Ok(Setzen(p, false, Christian, Typ(T110, "leichtbau"), Typ(TGis, "installationsvorwand")));
        Assert.NotNull(e.PaketId);
        var tm1 = Hole(p, "TM-1");
        Assert.Equal(("Typmaterial", T110, T110, "leichtbau"), (tm1.Klasse, tm1.Name, tm1.Text("typname"), tm1.Text("kategorie")));
        Assert.Equal(Christian, tm1.Eigenschaft("kategorie")!.Herkunft.Quelle);
        Assert.Equal(Christian, tm1.Eigenschaft("typname")!.Herkunft.Quelle);
        Assert.Equal("installationsvorwand", Hole(p, "TM-2").Text("kategorie"));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Element.StartsWith("TM-"));   // vollständig (Pflichteigenschaften)
        // ändern (eigene Quelle je Eintrag), dann unverändert
        var ae = Ok(Setzen(p, false, null, Typ(T110, "Mauerwerk", Fiktiv)));
        Assert.Contains(ae.Hinweise, h => h.StartsWith($"TM-1: «{T110}» → mauerwerk (Quelle: {Fiktiv}; war leichtbau) – Rohbau: Aussparung"));
        Assert.Equal(Fiktiv, Hole(p, "TM-1").Eigenschaft("kategorie")!.Herkunft.Quelle);
        var gleich = Ok(Setzen(p, false, null, Typ(T110, "mauerwerk", Fiktiv)));
        Assert.Empty(gleich.Aenderungen);
        Assert.Null(gleich.PaketId);
        Assert.Contains("Keine Änderung.", gleich.Hinweise);
        // entfernen (kategorie null), rückgängig
        var weg = Ok(Setzen(p, false, null, Typ(T110, null)));
        Assert.Null(p.Modell.Finde("TM-1"));
        Assert.Contains(weg.Hinweise, h => h.StartsWith($"TM-1: «{T110}» aus der Typenliste entfernt"));
        Assert.Contains(Ok(Setzen(p, false, null, Typ("FIKTIV gibt es nicht", ""))).Hinweise, h => h.Contains("nicht in der Typenliste"));
        Ok(p.Rueckgaengig());
        Assert.Equal("mauerwerk", Hole(p, "TM-1").Text("kategorie"));
        Ok(p.Rueckgaengig());
        Assert.Equal("leichtbau", Hole(p, "TM-1").Text("kategorie"));
        // ein Typ, der nirgends vorkommt: Hinweis auf die Schreibweise
        var falsch = Ok(Setzen(p, false, Fiktiv, Typ("2.3_IW 1.5/8/1.5 cm 1100", "leichtbau")));
        Assert.Contains(falsch.Hinweise, h => h.Contains("passt auf kein Bauteil") && h.Contains("Schreibweise prüfen"));
    }

    [Fact]
    public void Setzen_Fehler_AlleGenannt_MitVorschlag_NichtsGeaendert()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        var n = Nein(Setzen(p, false, null, Typ(T110, "leichtbau"), Typ("Typ B", "gips", Fiktiv), Typ("", "holz", Fiktiv), Typ("typ b", "holz", Fiktiv)));
        Assert.Contains($"«{T110}»: Quelle fehlt", n.Fehler);
        Assert.Contains("«Typ B»: Kategorie «gips» gibt es nicht (erlaubt: beton, mauerwerk, leichtbau, holz, stahl, daemmung, bekleidung, installationsvorwand, " +
                        "bodenaufbau, luftschicht)", n.Fehler);
        Assert.Contains("Eintrag 3: typname fehlt", n.Fehler);
        Assert.Contains("«typ b» doppelt", n.Fehler);
        Assert.Empty(p.Modell.DerKlasse("Typmaterial"));
        Assert.Contains("kategorie fehlt", Nein(p.Ausfuehren("typmaterial_setzen", new JsonObject { ["typen"] = new JsonArray(new JsonObject { ["typname"] = "X" }), ["quelle"] = Fiktiv })).Fehler);
        Nein(p.Ausfuehren("typmaterial_setzen", new JsonObject { ["typen"] = new JsonArray() }));
        Nein(p.Ausfuehren("typmaterial_setzen", new JsonObject { ["typen"] = "X" }));
        // «dämmung» wird zu daemmung
        Ok(Setzen(p, false, Fiktiv, Typ("Typ D", "Dämmung")));
        Assert.Equal("daemmung", Hole(p, "TM-1").Text("kategorie"));
    }

    [Fact]
    public void Katalog_KategorienWieTypenliste_PaarVorwandZuschaltbar()
    {
        var p = IfcHilfe.Leer();
        var d = p.Katalog.Klasse(Typenliste.Klasse);
        Assert.Equal(Typenliste.Kuerzel, d.Kuerzel);
        Assert.Equal(Typenliste.Kategorien.Keys, d.Eigenschaften.Single(x => x.Name == "kategorie").Auswahl!);
        Assert.True(d.Eigenschaften.Single(x => x.Name == "typname").Pflicht);
        Assert.Equal(["beton", "mauerwerk"], Typenliste.Kategorien.Where(x => Materialwortliste.Rohbau(x.Value)).Select(x => x.Key));
        Assert.Contains(KollisionTexte.HlksVorwand, KollisionTexte.Paare);
        Assert.DoesNotContain(KollisionTexte.HlksVorwand, KollisionTexte.PaareVorgabe);
        Assert.Contains("hlks-vorwand", KollisionTexte.ParPaare);
        Assert.Equal("installationsvorwand", Typenliste.KategorieVon(Materialgruppe.Installationsvorwand));
        Assert.Equal("stahl", Typenliste.KategorieVon(Materialgruppe.Metall));
        Assert.Null(Typenliste.KategorieVon(Materialgruppe.OhneAussage));
    }

    // ------------------------------------------------------------------ Abfrage typmaterial: unbekannte Typen

    [Fact]
    public void Abfrage_UnbekannteTypen_MitBauteilenAussparungenDurchgaengen_AusTypname_DanachInDerListe()
    {
        var p = Hintergrund(
            Wand("3W110", 0, 110, "DirectShape:" + T110, "Farbe, weiss"),
            Wand("3W110B", 0, 110, "DirectShape:" + T110, "Farbe, weiss") with { Punkte = NetzVersetzt(0, 5000, 110) },
            Wand("3WGIS", 3000, 50, "DirectShape:" + TGis, "<Unnamed>"),
            Wand("3WUNB", 6000, 200, "FIKTIV:Wandtyp A 200", "Farbe, weiss"),
            Wand("3WBET", 9000, 200, "FIKTIV:Beton, Stahlbeton 200", "<Unnamed>"));
        foreach (var (kz, x) in new[] { ("RO-1", 1000.0), ("RO-2", 4000), ("RO-3", 7000), ("RO-4", 10000) }) RohrY(p, kz, x);
        Ok(p.Ausfuehren("kollision_pruefen"));   // zuerst: mit Aussparung wäre der Durchgang keine Kollision
        Assert.Equal(3, Kollisionen(p).Count);                                           // die Vorwand nur im Paar hlks-vorwand
        var v1 = Ok(Vorschlagen(p));
        // ohne Typenliste: 110 und Wandtyp A unbekannt (Aussparung), GIS = Vorwand aus Typname (keine), Beton aus Typname (Aussparung)
        Assert.Equal(["RO-1", "RO-3", "RO-4"], Aussparungen(p).SelectMany(a => LeitungKz(p, a)).Order());
        Assert.Equal("Material der 4 gequerten Bauteile: 0 über die Typenliste (0 Einträge), 2 aus dem Typnamen " +
                     $"({Materialwortliste.EntscheidTypname}); 2 unbekannt – typmaterial zeigt die Typen zum Festlegen.", v1.Hinweise[1]);
        Assert.Contains("(kein Rohbau: Installationsvorwand 1)", v1.Hinweise[0]);

        var o = Abfrage(p);
        Assert.Empty(o["typen"]!.AsArray());
        var z110 = Zeile(o, "unbekannt", T110);
        Assert.Equal((2, 1, 1), (z110["bauteile"]!.GetValue<int>(), z110["aussparungen"]!.GetValue<int>(), z110["durchgaenge"]!.GetValue<int>()));
        Assert.Equal(["DirectShape:" + T110], z110["typnamen"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("IfcWall 2", z110["entitaeten"]!.GetValue<string>());
        Assert.Equal("Farbe, weiss 2", z110["material"]!.GetValue<string>());
        Assert.Equal("Wandtyp A 200", o["unbekannt"]!.AsArray()[1]!["schluessel"]!.GetValue<string>());   // nach Betroffenheit, dann Anzahl
        var zGis = Zeile(o, "ausTypname", TGis);
        Assert.Equal(("Installationsvorwand", "installationsvorwand", false), (zGis["gruppe"]!.GetValue<string>(), zGis["kategorie"]!.GetValue<string>(), zGis["aussparung"]!.GetValue<bool>()));
        Assert.Null(zGis["annahme"]);                                                    // Nachtrag B3e: bestätigt, keine ANNAHME mehr
        Assert.Equal(Materialwortliste.EntscheidVorwand, zGis["vermerk"]!.GetValue<string>());
        Assert.Equal("Beton", Zeile(o, "ausTypname", "Beton, Stahlbeton 200")["gruppe"]!.GetValue<string>());
        var zs = o["zusammenfassung"]!;
        Assert.Equal((5, 2, 3), (zs["bauteile"]!.GetValue<int>(), zs["unbekannteTypen"]!.GetValue<int>(), zs["unbekannt"]!.GetValue<int>()));
        Assert.Equal(2, zs["jeHerkunft"]!["typname"]!.GetValue<int>());
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Die Typenliste ist leer"));
        // betroffen: nur Typen mit Aussparungen oder Durchgängen
        var ob = Abfrage(p, betroffen: true);
        Assert.Equal(2, ob["unbekannt"]!.AsArray().Count);
        Assert.Equal(["Beton, Stahlbeton 200"], ob["ausTypname"]!.AsArray().Select(x => x!["schluessel"]!.GetValue<string>()));

        // festlegen (Christian 19.10.2026) – danach stehen beide in der Liste, mit Wirkung und Zahlen
        Ok(Setzen(p, false, Christian, Typ(T110, "leichtbau"), Typ(TGis, "installationsvorwand")));
        var v2 = Ok(Vorschlagen(p));
        Assert.Equal(["RO-3", "RO-4"], Aussparungen(p).SelectMany(a => LeitungKz(p, a)).Order());   // Leichtbau: Vorschlag gelöscht
        Assert.StartsWith("Material der 4 gequerten Bauteile: 2 über die Typenliste (2 Einträge), 1 aus dem Typnamen", v2.Hinweise[1]);
        Assert.Contains(v2.Hinweise, h => h.Contains("gelöscht: kein Beton bzw. Mauerwerk") && h.Contains($"Leichtbau: Typenliste «DirectShape:{T110}»"));
        var o2 = Abfrage(p);
        var t1 = o2["typen"]!.AsArray().Single(x => x!["element"]!.GetValue<string>() == "TM-1")!;
        Assert.Equal((T110, "leichtbau", 2, 0, 1), (t1["typname"]!.GetValue<string>(), t1["kategorie"]!.GetValue<string>(),
            t1["bauteile"]!.GetValue<int>(), t1["aussparungen"]!.GetValue<int>(), t1["durchgaenge"]!.GetValue<int>()));
        Assert.Equal(Christian, t1["quelle"]!.GetValue<string>());
        Assert.False(t1["aussparung"]!.GetValue<bool>());
        Assert.Equal(["Wandtyp A 200"], o2["unbekannt"]!.AsArray().Select(x => x!["schluessel"]!.GetValue<string>()));
        Assert.Equal(3, o2["zusammenfassung"]!["jeHerkunft"]!["typenliste"]!.GetValue<int>());
        Assert.Equal(10, o2["kategorien"]!.AsArray().Count);                             // mit bodenaufbau und luftschicht (Nachtrag B3e)
    }

    /// <summary>Punkte eines Quaders x0 … x0+2000, y0 … y0+dicke, z 0 … 2600 (für eine zweite Wand desselben Typs).</summary>
    static float[] NetzVersetzt(double x0, double y0, double dicke) =>
        S.Quader("X", "IfcWall", [x0, y0, 0], [x0 + 2000, y0 + dicke, 2600]).Punkte;

    // ------------------------------------------------------------------ Herkunft «aus Typname» in den Listen

    [Fact]
    public void HerkunftAusTypname_InAussparungslisteUndKollisionsliste()
    {
        var p = Hintergrund(Wand("3WBET", 0, 200, "FIKTIV:Beton, Stahlbeton 200", "Farbe, weiss"));
        RohrY(p, "RO-1", 1000);
        Ok(Vorschlagen(p));
        var a = VonLeitung(p, "RO-1");
        Assert.Equal("Beton: aus Typname «FIKTIV:Beton, Stahlbeton 200»", a.Text("material"));
        Assert.Contains(Materialwortliste.EntscheidTypname, a.Eigenschaft("material")!.Herkunft.Quelle);
        Assert.False(a.Eigenschaft("materialUnbekannt")!.Inhalt!.GetValue<bool>());
        var zeile = ((JsonObject)Tuer.Abfrage(p, "aussparungsliste"))["zeilen"]![0]!;
        Assert.Equal("Beton: aus Typname «FIKTIV:Beton, Stahlbeton 200»", zeile["material"]!.GetValue<string>());
        // Aussparung von Hand zurück auf vorgeschlagen lassen, damit die Durchdringung gemeldet wird: Aussparung löschen
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = a.Kennzeichen }));
        Ok(p.Ausfuehren("kollision_pruefen"));
        var kol = Assert.Single(Kollisionen(p));
        Assert.Equal(("durchdringung", "ja"), (kol.Text("typ"), kol.Text("rohbau")));
        Assert.Equal("Beton: aus Typname «FIKTIV:Beton, Stahlbeton 200»", kol.Text("material"));
        Assert.Contains(Materialwortliste.EntscheidTypname, kol.Eigenschaft("material")!.Herkunft.Quelle);
        var kz = ((JsonObject)Tuer.Abfrage(p, "kollisionen"))["zeilen"]![0]!;
        Assert.Equal("Beton: aus Typname «FIKTIV:Beton, Stahlbeton 200»", kz["material"]!.GetValue<string>());
        Assert.Equal("hlks-architektur", kz["paar"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ leichtbau: keine Aussparung, Kollision ohne P20

    [Fact]
    public void Leichtbau_KeineAussparung_FesteBleibt_KollisionOhneP20()
    {
        var p = Hintergrund(Wand("3W110", 0, 110, "DirectShape:" + T110, "Farbe, weiss"));
        RohrY(p, "RO-1", 1000);
        RohrY(p, "RO-2", 1500);
        Ok(Vorschlagen(p));
        var alle = Aussparungen(p);
        Assert.All(alle, a => Assert.True(a.Eigenschaft("materialUnbekannt")!.Inhalt!.GetValue<bool>()));
        var fest = VonLeitung(p, "RO-2");
        Ok(Status(p, "freigegeben", fest.Kennzeichen));

        Ok(Setzen(p, false, Christian, Typ(T110, "leichtbau")));
        var e = Ok(Vorschlagen(p));
        // Handrechnung: RO-1 und RO-2 liegen 500 mm auseinander (Rand 500 − 95 = 405 > 100) – zwei Aussparungen; die vorgeschlagene wird
        // gelöscht, die freigegebene bleibt (abgegeben)
        Assert.Equal([fest.Kennzeichen], Aussparungen(p).Select(a => a.Kennzeichen));
        Assert.Contains(e.Hinweise, h => h.StartsWith(fest.Kennzeichen) && h.Contains("bleibt, weil schon abgegeben"));
        Assert.Contains("(kein Rohbau: Leichtbau 1)", e.Hinweise[0]);
        Ok(p.Ausfuehren("kollision_pruefen"));
        var kol = Kollisionen(p).Single(k => KollisionspruefungHilfe.Beteiligte(k).Contains(Hole(p, "RO-1").Id));
        Assert.Equal(("durchdringung", "nein"), (kol.Text("typ"), kol.Text("rohbau")));
        Assert.Equal($"Leichtbau: Typenliste «DirectShape:{T110}»", kol.Text("material"));
        Assert.Contains("Typenliste TM-1", kol.Eigenschaft("material")!.Herkunft.Quelle);
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P20");
    }

    // ------------------------------------------------------------------ installationsvorwand: keine Aussparung, keine Kollision

    [Fact]
    public void Installationsvorwand_KeineAussparung_KeineKollision_MitPaarHlksVorwandWieder()
    {
        // FIKTIVER Typname ohne GIS/Vorwand: erst die Typenliste macht ihn zur Installationsvorwand
        var p = Hintergrund(Wand("3WVW", 0, 50, "FIKTIV:IW S 1.8/3.2 cm 50", "Farbe, weiss"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        RohrY(p, "RO-1", 1000);
        Ok(p.Ausfuehren("kollision_pruefen"));
        var kol = Assert.Single(Kollisionen(p));
        Assert.Equal(("durchdringung", "unbekannt"), (kol.Text("typ"), kol.Text("rohbau")));
        Ok(KollisionspruefungHilfe.Status(p, "bestaetigt", "FIKTIV: Vorwand?", kol.Kennzeichen));
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P20");

        Ok(Setzen(p, false, Fiktiv, Typ("IW S 1.8/3.2 cm 50", "installationsvorwand")));
        // Vorgabe-Paare: die Vorwand wird nicht geprüft – die alte Kollision bleibt unverändert (ausserhalb), Material nachgeführt → kein P20
        var e1 = Ok(p.Ausfuehren("kollision_pruefen"));
        var nachher = Hole(p, kol.Kennzeichen);
        Assert.Equal("bestaetigt", nachher.Text("status"));
        Assert.DoesNotContain("nicht mehr gefunden", nachher.Text("pruefung"));
        Assert.Equal(("nein", "Installationsvorwand: Typenliste «FIKTIV:IW S 1.8/3.2 cm 50»"), (nachher.Text("rohbau"), nachher.Text("material")));
        Assert.Contains("ausserhalb des Laufs nachgeführt", nachher.Eigenschaft("material")!.Herkunft.Quelle);
        Assert.Contains("1 ausserhalb dieses Laufs (unverändert)", KollisionspruefungHilfe.Hinweis(e1, "Status:"));
        Assert.Contains("Davon 1 mit Installationsvorwänden: Paar hlks-vorwand nicht gewählt", KollisionspruefungHilfe.Hinweis(e1, "Installationsvorwänden"));
        Assert.Contains("1 Durchdringung(en) ausserhalb dieses Laufs: Material und rohbau nachgeführt", KollisionspruefungHilfe.Hinweis(e1, "nachgeführt"));
        Assert.Contains("1 Installationsvorwand/-vorwände (nur im Paar hlks-vorwand)", KollisionspruefungHilfe.Hinweis(e1, "Typenliste (1 Einträge)"));
        Assert.Contains("hart 0", KollisionspruefungHilfe.Hinweis(e1, "Prüflauf"));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P20");
        // Liste: Paar hlks-vorwand
        var liste = (JsonObject)Tuer.Abfrage(p, "kollisionen");
        Assert.Equal("hlks-vorwand", liste["zeilen"]![0]!["paar"]!.GetValue<string>());
        Assert.Equal(1, liste["zusammenfassung"]!["jePaar"]!["hlks-vorwand"]!.GetValue<int>());
        Assert.Single(((JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["paar"] = "hlks-vorwand" }))["zeilen"]!.AsArray());
        // ein weiterer Lauf ändert nichts (nur der Zähler)
        var e2 = Ok(p.Ausfuehren("kollision_pruefen"));
        Assert.Single(e2.Aenderungen);
        // zugeschaltet: die Vorwand wird wieder geprüft – dieselbe Kollision (Status bleibt), Durchdringung ohne P20
        var e3 = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("hlks-architektur", "hlks-vorwand") }));
        var wieder = Assert.Single(Kollisionen(p));
        Assert.Equal((kol.Kennzeichen, "bestaetigt", "durchdringung", "nein"), (wieder.Kennzeichen, wieder.Text("status"), wieder.Text("typ"), wieder.Text("rohbau")));
        Assert.Equal($"{ul.Id}:{G("3WVW")}", wieder.Text("bauteil"));
        Assert.Contains("hart 1 (durchdringung 1", KollisionspruefungHilfe.Hinweis(e3, "  hlks-vorwand:"));
        Assert.Contains("1 wieder gefunden", KollisionspruefungHilfe.Hinweis(e3, "Status:"));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P20");
        // keine Aussparung
        var v = Ok(Vorschlagen(p));
        Assert.Empty(Aussparungen(p));
        Assert.Contains("(kein Rohbau: Installationsvorwand 1)", v.Hinweise[0]);
        // Paar umgedreht geht auch
        Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("vorwand-hlks") }, true));
    }

    [Fact]
    public void InstallationsvorwandAusTypnameGis_OhneTypenliste_SchonKeineKollision()
    {
        var p = Hintergrund(Wand("3WGIS", 0, 50, "DirectShape:" + TGis, "<Unnamed>"), Wand("3WBET", 3000, 200, "FIKTIV:Beton 200", "Beton"));
        RohrY(p, "RO-1", 1000);
        RohrY(p, "RO-2", 4000);
        var e = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("hlks-architektur", "hlks-vorwand") }, true));
        Assert.Contains("hart 1 (durchdringung 1", KollisionspruefungHilfe.Hinweis(e, "  hlks-vorwand:"));
        Assert.Contains("hart 1 (durchdringung 1", KollisionspruefungHilfe.Hinweis(e, "  hlks-architektur:"));
        Ok(p.Ausfuehren("kollision_pruefen"));
        var kol = Assert.Single(Kollisionen(p));
        Assert.Equal("Beton: Beton", kol.Text("material"));
    }

    // ------------------------------------------------------------------ bekleidung laut Typenliste

    [Fact]
    public void BekleidungLautTypenliste_NurImPaarHlksBekleidung_KeineAussparung()
    {
        var p = Hintergrund(Wand("3WBEK", 0, 30, "FIKTIV:Vorsatz 30", "Farbe, weiss"));
        RohrY(p, "RO-1", 1000);
        Ok(Setzen(p, false, Fiktiv, Typ("Vorsatz 30", "bekleidung")));
        var e = Ok(p.Ausfuehren("kollision_pruefen"));
        Assert.Empty(Kollisionen(p));
        Assert.Contains("1 Bekleidung(en) laut Typenliste, 0 Unterdecke(n), 0 Bodenaufbau-Schicht(en) (nur im Paar hlks-bekleidung)",
            KollisionspruefungHilfe.Hinweis(e, "Typenliste (1 Einträge)"));
        Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("hlks-architektur", "hlks-bekleidung") }));
        var kol = Assert.Single(Kollisionen(p));
        Assert.Equal(("durchdringung", "nein"), (kol.Text("typ"), kol.Text("rohbau")));
        Assert.Equal("hlks-bekleidung", ((JsonObject)Tuer.Abfrage(p, "kollisionen"))["zeilen"]![0]!["paar"]!.GetValue<string>());
        Ok(Vorschlagen(p));
        Assert.Empty(Aussparungen(p));
        // wieder ohne das Paar: unverändert ausserhalb, als Bekleidung gezählt
        Assert.Contains("Davon 1 mit Bekleidungen", KollisionspruefungHilfe.Hinweis(Ok(p.Ausfuehren("kollision_pruefen")), "Bekleidungen (IfcCovering)"));
    }

    // ------------------------------------------------------------------ neue Architektenfassung, Doppel, ohne Quelle

    [Fact]
    public void NeueArchitektenfassung_GleicherTypname_ListeGiltWeiter()
    {
        var p = Hintergrund(Wand("3ALT", 0, 110, "DirectShape:" + T110, "Farbe, weiss"));
        Ok(Setzen(p, false, Christian, Typ(T110, "leichtbau")));
        Materialbefund Einzig() => Bauteilmaterial.Von(p.Modell, p.Katalog, Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Entitaet == "IfcWall"));
        Assert.Equal(Materialherkunft.Typenliste, Einzig().Herkunft);
        // neue Fassung: andere GlobalId, gleicher Typ
        S.AlsNetzDerUnterlage(p, 3, [Wand("3NEU", 500, 110, "DirectShape:" + T110, "Farbe, weiss")]);
        var neu = Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Entitaet == "IfcWall");
        Assert.Equal(G("3NEU"), neu.GlobalId);
        var b = Bauteilmaterial.Von(p.Modell, p.Katalog, neu);
        Assert.Equal((Materialherkunft.Typenliste, true), (b.Herkunft, b.KeinRohbau));
    }

    [Fact]
    public void Doppelt_ZuletztGesetztGilt_SetzenRaeumtAuf_OhneQuelleGiltNicht()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        JsonObject Eig(string typname, string kategorie, string? quelle) => new()
        {
            ["typname"] = typname,
            ["kategorie"] = quelle is null ? kategorie : new JsonObject { ["wert"] = kategorie, ["quelle"] = quelle },
        };
        // zwei Rechner legen denselben Typ an (hier: element_anlegen mit Quelle), ein dritter Eintrag ohne Quelle
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Typmaterial", ["kennzeichen"] = "TM-1", ["eigenschaften"] = Eig("Typ D", "holz", Fiktiv) }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Typmaterial", ["kennzeichen"] = "TM-2", ["eigenschaften"] = Eig("typ  d", "beton", Fiktiv) }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Typmaterial", ["kennzeichen"] = "TM-3", ["eigenschaften"] = Eig("Typ O", "beton", null) }));
        var liste = Typenliste.Lesen(p.Modell);
        var (gilt, weitere) = Assert.Single(liste.Doppelt);
        Assert.Single(weitere);
        Assert.Contains(gilt.Kennzeichen, new[] { "TM-1", "TM-2" });
        Assert.Equal(["TM-3"], liste.OhneQuelle.Select(e => e.Kennzeichen));
        Assert.Null(liste.Finde("Typ O"));                                               // ohne Quelle gilt nicht
        var o = Abfrage(p);
        Assert.Equal(["TM-3"], o["ohneQuelle"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Single(o["doppelt"]!.AsArray());
        var hinweise = o["hinweise"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains(hinweise, h => h.Contains("steht mehrfach") && h.Contains("typmaterial_setzen"));
        Assert.Contains(hinweise, h => h.StartsWith("TM-3 «Typ O» ohne Quelle – gilt nicht"));
        // setzen räumt auf: ein Eintrag bleibt
        var e = Ok(Setzen(p, false, Fiktiv, Typ("Typ D", "mauerwerk"), Typ("Typ O", "beton")));
        Assert.Contains(e.Hinweise, h => h.Contains("Doppelte Einträge") && h.Contains("gelöscht"));
        var nachher = Typenliste.Lesen(p.Modell);
        Assert.Empty(nachher.Doppelt);
        Assert.Empty(nachher.OhneQuelle);
        Assert.Equal(["mauerwerk", "beton"], new[] { nachher.Finde("Typ D")!.Kategorie, nachher.Finde("Typ O")!.Kategorie });
        Assert.Equal(2, p.Modell.DerKlasse("Typmaterial").Count());
    }

    [Fact]
    public void KicadBauteilOhneTypname_TypenlisteGiltNicht_AbfrageZaehltOhneTypname()
    {
        var p = Rechteck();
        projekte.Add(p);
        Ok(Setzen(p, false, Fiktiv, Typ("Wand", "beton")));
        var w = Pruefkoerperbau.Erzeugen(p).Koerper.Single(k => k.Kennzeichen == "W-1");
        var b = Bauteilmaterial.Von(p.Modell, p.Katalog, w);
        Assert.True(b.Unbekannt);
        Assert.Equal("unbekannt: W-1 ohne material und Aufbau", b.Text);
        var o = Abfrage(p);
        Assert.Equal(4, o["zusammenfassung"]!["unbekanntOhneTypname"]!.GetValue<int>());
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("4 Bauteil(e) mit unbekanntem Material ohne Typname"));
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("TM-1 «Wand» passt auf kein Bauteil"));
    }
}
