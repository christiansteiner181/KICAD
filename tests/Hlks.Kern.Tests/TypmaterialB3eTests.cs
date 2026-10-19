using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;
using S = Hlks.Kern.Tests.NetzdateiTestschreiber;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag B3e (Entscheide Christian 19.10.2026 zu B3d, Fragefenster; entwurf/modul-3b.md): die drei Annahmen aus B3d sind
/// Entscheide; Unterdecken wie Bekleidung (Wortliste, Paar hlks-bekleidung), Luftschichten ignorieren (weder Kollision noch Aussparung
/// noch P20), neue Kategorien bodenaufbau und luftschicht, Wortliste nur für querbare Bauteile, Zusammenfassung je Grund in den Hinweisen
/// von aussparungen_vorschlagen. Hintergrund-Bauteile als Quader in einer Netzdatei Fassung 3 von Hand; Rohre der Testserie DN 50 (da 55,
/// FIKTIV); Vorgaben FIKTIV (Spiel 20, Zusammenfassen 100 mm). Material- und Typnamen FIKTIV – ausser den echten Typnamen aus Christians
/// Entscheiden (Bernstrasse 42: «5.1_Geschossdecke UB 10/3/6/1.5 cm 205» = bodenaufbau, «Luft, horizontal (Teil)», «abgehängte Decke»).</summary>
public sealed class TypmaterialB3eTests : IDisposable
{
    const string Christian = "Christian Steiner, H+K Planungs AG, 19.10.2026";
    const string Fiktiv = "FIKTIV – Test B3e";
    const string TUb = "5.1_Geschossdecke UB 10/3/6/1.5 cm 205";
    const string T110 = "2.3_IW 1.5/8/1.5 cm 110";

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

    /// <summary>Wand bzw. Schicht x0 … x0+2000, y 0 … dicke, z 0 … 2600.</summary>
    static S.Satz Wand(string gid, double x0, double dicke, string entitaet, string typname, params string[] material) =>
        S.Quader(gid, entitaet, [x0, 0, 0], [x0 + 2000, dicke, 2600], materialien: material, typname: typname);

    /// <summary>Decke x0 … x0+2000, y 0 … 2000, z 2600 … 2600+dicke.</summary>
    static S.Satz Decke(string gid, double x0, double dicke, string typname, params string[] material) =>
        S.Quader(gid, "IfcSlab", [x0, 0, 2600], [x0 + 2000, 2000, 2600 + dicke], materialien: material, typname: typname);

    static void RohrY(Projekt p, string kz, double x) => Ok(Modul3Hilfe.Rohr(p, kz, "DN 50", [x, -500, 1000], [x, 1000, 1000]));
    static void RohrZ(Projekt p, string kz, double x) => Ok(Modul3Hilfe.Rohr(p, kz, "DN 50", [x, 1000, 2000], [x, 1000, 3500]));

    static BefehlsErgebnis Setzen(Projekt p, string quelle, params (string Typname, string? Kategorie)[] typen) =>
        p.Ausfuehren("typmaterial_setzen", new JsonObject
        {
            ["typen"] = new JsonArray(typen.Select(t => (JsonNode)new JsonObject { ["typname"] = t.Typname, ["kategorie"] = t.Kategorie }).ToArray()),
            ["quelle"] = quelle,
        });

    static List<Element> Kollisionen(Projekt p) => KollisionspruefungHilfe.Kollisionen(p);
    static int P20(Projekt p) => Pruefungen.Alle(p.Modell, p.Katalog).Count(b => b.Code == "P20");
    static JsonObject Pruefen(Projekt p, params string[] paare) =>
        paare.Length == 0 ? new JsonObject() : new JsonObject { ["paare"] = KollisionspruefungHilfe.Par(paare) };
    static Materialangabe A(string text, bool typ = false) => new(text, "Test", typ);
    static Materialbefund Von(Projekt p, string? typ, string entitaet, params string[] mat) =>
        Bauteilmaterial.Von(p.Modell, p.Katalog, new Bauteilangaben(null, mat, typ, null, 3, false, entitaet));

    // ------------------------------------------------------------------ Wortliste Unterdecke (Entscheid; Kühldecke ANNAHME)

    [Theory]
    [InlineData("DirectShape:abgehängte Decke 6.5 cm 65", Materialgruppe.Unterdecke)]   // Befund Bernstrasse (IfcSlab)
    [InlineData("Abgehaengte Decke", Materialgruppe.Unterdecke)]
    [InlineData("Unterdecke Gipskarton 12.5", Materialgruppe.Unterdecke)]               // vor Leichtbau: die Funktion zählt
    [InlineData("Akustikdecke Gips", Materialgruppe.Unterdecke)]
    [InlineData("Deckenbekleidung Holz", Materialgruppe.Unterdecke)]                     // vor Holz
    [InlineData("Deckenverkleidung Metall", Materialgruppe.Unterdecke)]                  // ANNAHME (Ergänzung)
    [InlineData("Rasterdecke 625", Materialgruppe.Unterdecke)]
    [InlineData("Metallkühldecke", Materialgruppe.Unterdecke)]                           // ANNAHME, vor Metall
    [InlineData("Kuehldecke", Materialgruppe.Unterdecke)]
    [InlineData("Suspended ceiling 600x600", Materialgruppe.Unterdecke)]
    [InlineData("False ceiling", Materialgruppe.Unterdecke)]
    [InlineData("Faux-plafond", Materialgruppe.Unterdecke)]
    [InlineData("Plafond suspendu", Materialgruppe.Unterdecke)]
    [InlineData("Unterdecke Beton", Materialgruppe.Beton)]                               // Rohbau geht vor
    [InlineData("Wandbekleidung", Materialgruppe.Ausbau)]                                // nur Decken-Bekleidung ist Unterdecke
    public void Wortliste_Unterdecke_NachBetonVorDenBaustoffen(string name, Materialgruppe erwartet) =>
        Assert.Equal(erwartet, Materialwortliste.Gruppe(name));

    // ------------------------------------------------------------------ Wortliste Bodenaufbau (ANNAHME; «UB» nicht)

    [Theory]
    [InlineData("Unterlagsboden 80", Materialgruppe.Bodenaufbau)]
    [InlineData("Zementestrich", Materialgruppe.Bodenaufbau)]
    [InlineData("Anhydrit-Fliessestrich 50", Materialgruppe.Bodenaufbau)]
    [InlineData("Fussbodenaufbau 120", Materialgruppe.Bodenaufbau)]
    [InlineData("Trittschalldämmung 20", Materialgruppe.Bodenaufbau)]                   // vor Dämmung
    [InlineData("Floor screed", Materialgruppe.Bodenaufbau)]
    [InlineData("Chape ciment", Materialgruppe.Bodenaufbau)]
    [InlineData("Stahlbeton mit Unterlagsboden", Materialgruppe.Beton)]                // Rohbau geht vor
    [InlineData("UB 80", null)]                                                          // «UB» zu kurz – Typenliste je Projekt
    [InlineData("DirectShape:" + TUb, null)]
    [InlineData("Chapeau", null)]                                                        // nur ganzes Wort «chape»
    public void Wortliste_Bodenaufbau_OhneUB(string name, Materialgruppe? erwartet) =>
        Assert.Equal(erwartet, Materialwortliste.Gruppe(name));

    // ------------------------------------------------------------------ Wortliste Luftschicht (Entscheid; nur ganzes Wort)

    [Theory]
    [InlineData("Luft, horizontal (Teil)")]
    [InlineData("DirectShape:Luft, vertikal")]
    [InlineData("Luft")]
    [InlineData("Luft_40")]
    [InlineData("Luftschicht 40 mm")]
    [InlineData("Luftschichten")]
    [InlineData("Hinterlüftung 30 mm")]
    [InlineData("Air gap 25")]
    [InlineData("Air-space")]
    [InlineData("Cavity")]
    [InlineData("Lame d'air 20")]
    [InlineData("Lame d’air")]
    public void Wortliste_Luftschicht(string name) => Assert.Equal(Materialgruppe.Luftschicht, Materialwortliste.Gruppe(name));

    [Theory]
    [InlineData("Luftkanal 400x200")]
    [InlineData("Zuluft")]
    [InlineData("Abluftkanal")]
    [InlineData("Lüftungsgitter")]
    [InlineData("Luft-Wasser-Wärmepumpe")]
    [InlineData("Wasser/Luft")]
    [InlineData("Frischluftfassung")]
    [InlineData("Luftspalt")]
    [InlineData("Cavity wall")]
    [InlineData("Brick cavity wall")]
    public void Wortliste_Luftschicht_NurGanzesWort_LuftkanalUndCoTreffenNicht(string name) =>
        Assert.NotEqual(Materialgruppe.Luftschicht, Materialwortliste.Gruppe(name));

    // ------------------------------------------------------------------ Einteilung, Vermerke, Entscheide

    [Fact]
    public void Einteilung_HauptgruppeUndAussagekraft_LuftschichtNurAllein_VermerkeEntscheidBzwAnnahme()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        // Entscheid (a): Bodenaufbau nennt keine Bauart → der Typname zählt mit; nennt er Beton, gibt es eine Aussparung
        Assert.True(Von(p, "Geschossdecke Beton 250", "IfcSlab", "Zementestrich").Rohbau);
        var bo = Von(p, "FIKTIV Decke 7", "IfcSlab", "Zementestrich");
        Assert.True(bo.Bodenaufbau); Assert.True(bo.AlsBekleidung); Assert.True(bo.KeinRohbau);
        Assert.Equal(("Bodenaufbau: Zementestrich", Materialherkunft.Material), (bo.Text, bo.Herkunft));
        Assert.Contains(Materialwortliste.AnnahmeBodenaufbau, bo.Quelle);
        Assert.Contains("ANNAHME", Materialwortliste.AnnahmeBodenaufbau);
        Assert.Contains("Dachboden", Materialwortliste.AnnahmeBodenaufbau);               // «Estrich» heisst in der Schweiz auch Dachboden
        // Luftschicht aus dem Typnamen (Revit-Teil, Material nur Darstellung)
        var lu = Von(p, "Luft, horizontal (Teil)", Pruefkoerperbau.Teilentitaet, "Farbe, weiss");
        Assert.True(lu.Luftschicht); Assert.False(lu.AlsBekleidung); Assert.True(lu.KeinRohbau);
        Assert.Equal(("Luftschicht: aus Typname «Luft, horizontal (Teil)»", Materialherkunft.Typname), (lu.Text, lu.Herkunft));
        Assert.Contains(Materialwortliste.EntscheidLuftschicht, lu.Quelle);
        Assert.DoesNotContain("ANNAHME Prüfliste (Nachtrag B3e)", lu.Quelle);
        // eine Luftschicht ist ein Bauteil nur, wenn sonst nichts erkannt ist
        Assert.True(Materialbefund.Einteilen([A("Luft")]).Luftschicht);
        var gk = Materialbefund.Einteilen([A("Gipskarton"), A("Luft")]);
        Assert.Equal((Materialgruppe?)Materialgruppe.Leichtbau, gk.Gruppe);
        Assert.False(gk.Luftschicht);
        Assert.True(Materialbefund.Einteilen([A("Kalksandstein"), A("Luft"), A("Klinker")]).Rohbau);
        // Rang ohne Rohbau: Bauart, dann Unterdecke/Bodenaufbau, dann Ausbau, dann Luftschicht
        var ud = Materialbefund.Einteilen([A("Kalkputz"), A("Akustikdecke")]);
        Assert.Equal((Materialgruppe?)Materialgruppe.Unterdecke, ud.Gruppe);
        Assert.True(ud.AlsBekleidung);
        Assert.Equal((Materialgruppe?)Materialgruppe.Daemmung, Materialbefund.Einteilen([A("Zementestrich"), A("Mineralwolle")]).Gruppe);
        Assert.Equal((Materialgruppe?)Materialgruppe.Ausbau, Materialbefund.Einteilen([A("Luft"), A("Kalkputz")]).Gruppe);
        // übriger Ausbau aus der Wortliste (Glas, Putz) bleibt im Paar hlks-architektur
        var glas = Materialbefund.Einteilen([A("Glas")]);
        Assert.False(glas.AlsBekleidung);
        Assert.Equal(KollisionTexte.HlksArchitektur, Kollisionspaar.Architektur(false, glas));
        Assert.Equal(KollisionTexte.HlksBekleidung, Kollisionspaar.Architektur(false, ud));
        // Vermerke: Unterdecke Entscheid, Kühldecke dazu ANNAHME; Installationsvorwand Entscheid (B3d-Annahme bestätigt)
        Assert.Equal(Materialwortliste.EntscheidUnterdecke, Materialbefund.Einteilen([A("Unterdecke")]).Vermerk);
        Assert.Equal($"{Materialwortliste.EntscheidUnterdecke}; {Materialwortliste.AnnahmeUnterdecke}", Materialbefund.Einteilen([A("Kühldecke")]).Vermerk);
        Assert.Contains("Kühldecke", Materialwortliste.AnnahmeUnterdecke);
        Assert.Equal(Materialwortliste.EntscheidVorwand, Materialbefund.Einteilen([A("Vorwand 25")]).Vermerk);
        Assert.Null(Materialbefund.Einteilen([A("Gipskarton")]).Vermerk);
        Assert.Null(Materialbefund.Einteilen([A("Stahlbeton")]).Vermerk);
    }

    [Fact]
    public void Querbar_WortlisteNurFuerQuerbareBauteile_TypenlisteImmer()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        var k = p.Katalog;
        Assert.True(Kollisionssuche.QuerbarNach(k, "Wand", null));
        Assert.True(Kollisionssuche.QuerbarNach(k, "Decke", "IfcDoor"));                 // mit Klasse zählt die Klasse
        Assert.False(Kollisionssuche.QuerbarNach(k, "Fenster", null));
        Assert.False(Kollisionssuche.QuerbarNach(k, "Tuer", null));
        Assert.True(Kollisionssuche.QuerbarNach(k, null, Pruefkoerperbau.Teilentitaet));
        Assert.True(Kollisionssuche.QuerbarNach(k, null, "IfcSlab"));
        Assert.False(Kollisionssuche.QuerbarNach(k, null, "IfcDoor"));
        Assert.False(Kollisionssuche.QuerbarNach(k, null, "IfcStair"));
        // «Estrich» heisst in der Schweiz auch Dachboden: Estrichtür, Estrichtreppe bleiben im Paar hlks-architektur
        var tuer = Von(p, "Estrichtür 80/200", "IfcDoor", "<Unnamed>");
        Assert.Equal((Materialgruppe?)Materialgruppe.Bodenaufbau, tuer.Gruppe);
        Assert.False(tuer.Querbar); Assert.False(tuer.Bodenaufbau); Assert.False(tuer.AlsBekleidung);
        Assert.Equal(KollisionTexte.HlksArchitektur, Kollisionspaar.Architektur(false, tuer));
        Assert.False(Von(p, "Luft", "IfcStair", "<Unnamed>").Luftschicht);
        Assert.False(Von(p, "Akustikdecke", "IfcRailing", "<Unnamed>").AlsBekleidung);
        // querbar: Decke, Teil
        Assert.True(Von(p, "Zementestrich 80", "IfcSlab", "<Unnamed>").AlsBekleidung);
        Assert.True(Von(p, "Luft, vertikal", Pruefkoerperbau.Teilentitaet, "<Unnamed>").Luftschicht);
        // Typenliste: gilt immer (ausdrückliche Festlegung mit Quelle)
        Ok(Setzen(p, Fiktiv, ("FIKTIV Treppe 1", "bodenaufbau"), ("FIKTIV Tür 2", "luftschicht")));
        Assert.True(Von(p, "FIKTIV Treppe 1", "IfcStair", "<Unnamed>").AlsBekleidung);
        Assert.True(Von(p, "FIKTIV Tür 2", "IfcDoor", "<Unnamed>").Luftschicht);
    }

    // ------------------------------------------------------------------ Unterdecke: nur im Paar hlks-bekleidung, keine Aussparung

    [Fact]
    public void Unterdecke_AlsIfcSlab_NurImPaarHlksBekleidung_KeineAussparung_KeinP20()
    {
        var p = Hintergrund(Decke("3UD", 0, 65, "DirectShape:abgehängte Decke 6.5 cm 65", "Farbe, weiss"), Decke("3BET", 3000, 200, "FIKTIV:Decke 200", "Beton"));
        RohrZ(p, "RO-1", 1000);
        RohrZ(p, "RO-2", 4000);
        var e1 = Ok(p.Ausfuehren("kollision_pruefen"));
        var kol = Assert.Single(Kollisionen(p));                                         // nur die Betondecke
        Assert.Equal("Beton: Beton", kol.Text("material"));
        Assert.Contains("1 Unterdecke(n)", KollisionspruefungHilfe.Hinweis(e1, "Typenliste (0 Einträge)"));
        var v = Ok(Vorschlagen(p));
        Assert.Equal(["RO-2"], Aussparungen(p).SelectMany(a => LeitungKz(p, a)));
        Assert.Contains("(kein Rohbau: Unterdecke 1)", v.Hinweise[0]);
        // zugeschaltet: Durchdringung der Unterdecke im Paar hlks-bekleidung, rohbau nein, kein P20
        Ok(p.Ausfuehren("kollision_pruefen", Pruefen(p, "hlks-architektur", "hlks-bekleidung")));
        var ud = Kollisionen(p).Single(k => k.Text("material")?.StartsWith("Unterdecke") == true);
        Assert.Equal(("durchdringung", "nein", "neu"), (ud.Text("typ"), ud.Text("rohbau"), ud.Text("status")));
        Assert.Equal("Unterdecke: aus Typname «DirectShape:abgehängte Decke 6.5 cm 65»", ud.Text("material"));
        Assert.Contains(Materialwortliste.EntscheidUnterdecke, ud.Eigenschaft("material")!.Herkunft.Quelle);
        var zeile = ((JsonObject)Tuer.Abfrage(p, "kollisionen"))["zeilen"]!.AsArray().Single(z => z!["kollision"]!.GetValue<string>() == ud.Kennzeichen)!;
        Assert.Equal("hlks-bekleidung", zeile["paar"]!.GetValue<string>());
        Assert.Equal(0, P20(p));
        // wieder ohne das Paar: unverändert ausserhalb, als Bekleidung gezählt
        var e3 = Ok(p.Ausfuehren("kollision_pruefen"));
        Assert.Contains("Davon 1 mit Bekleidungen (IfcCovering) bzw. Unterdecken und Bodenaufbau", KollisionspruefungHilfe.Hinweis(e3, "Bekleidungen (IfcCovering)"));
        Assert.Equal("neu", Hole(p, ud.Kennzeichen).Text("status"));
    }

    // ------------------------------------------------------------------ Bodenaufbau: Typenliste (Christian), alte Kollision bleibt ausserhalb

    [Fact]
    public void Bodenaufbau_Typenliste_KeineKollision_AlteBleibtAusserhalbOhneP20_KeineAussparung()
    {
        var p = Hintergrund(Decke("3UB", 0, 120, "DirectShape:" + TUb, "Farbe, weiss"));
        RohrZ(p, "RO-1", 1000);
        Ok(p.Ausfuehren("kollision_pruefen"));
        var kol = Assert.Single(Kollisionen(p));
        Assert.Equal(("durchdringung", "unbekannt"), (kol.Text("typ"), kol.Text("rohbau")));   // «UB» steht nicht in der Wortliste
        Assert.Equal(1, P20(p));

        var s = Ok(Setzen(p, Christian, (TUb, "bodenaufbau")));
        Assert.Contains(s.Hinweise, h => h.StartsWith($"TM-1: «{TUb}» → bodenaufbau (Quelle: {Christian}; neu) – keine Aussparung; Kollision nur im Paar hlks-bekleidung"));
        var e = Ok(p.Ausfuehren("kollision_pruefen"));
        var nachher = Hole(p, kol.Kennzeichen);
        Assert.Equal(("neu", "nein"), (nachher.Text("status"), nachher.Text("rohbau")));          // ausserhalb: Status bleibt, Material nachgeführt
        Assert.Equal($"Bodenaufbau: Typenliste «DirectShape:{TUb}»", nachher.Text("material"));
        Assert.Contains("hart 0", KollisionspruefungHilfe.Hinweis(e, "Prüflauf"));
        Assert.Contains("Davon 1 mit Bekleidungen (IfcCovering) bzw. Unterdecken und Bodenaufbau", KollisionspruefungHilfe.Hinweis(e, "Bekleidungen (IfcCovering)"));
        Assert.Contains("1 Bodenaufbau-Schicht(en) (nur im Paar hlks-bekleidung)", KollisionspruefungHilfe.Hinweis(e, "Typenliste (1 Einträge)"));
        Assert.Equal(0, P20(p));
        Assert.Equal("hlks-bekleidung", ((JsonObject)Tuer.Abfrage(p, "kollisionen"))["zeilen"]![0]!["paar"]!.GetValue<string>());
        var v = Ok(Vorschlagen(p));
        Assert.Empty(Aussparungen(p));
        Assert.Contains("(kein Rohbau: Bodenaufbau 1)", v.Hinweise[0]);
        // Abfrage: Wirkung und Kategorie
        var t = ((JsonObject)Tuer.Abfrage(p, "typmaterial"))["typen"]![0]!;
        Assert.Equal(("bodenaufbau", "Bodenaufbau", false), (t["kategorie"]!.GetValue<string>(), t["gruppe"]!.GetValue<string>(), t["aussparung"]!.GetValue<bool>()));
        Assert.Equal(Typenliste.Wirkung("bodenaufbau"), t["wirkung"]!.GetValue<string>());
    }

    // ------------------------------------------------------------------ Luftschicht: in keinem Paar; alte Kollision → behoben; kein P20

    [Fact]
    public void Luftschicht_WederKollisionNochAussparungNochP20_AlteKollisionBehoben_AbfrageMitKategorie()
    {
        var p = Hintergrund(
            Wand("3LH", 0, 40, Pruefkoerperbau.Teilentitaet, "Luft, horizontal (Teil)", "Farbe, weiss"),
            Wand("3TX", 3000, 40, Pruefkoerperbau.Teilentitaet, "FIKTIV Schicht X", "<Unnamed>"),
            Wand("3LW", 6000, 40, "IfcWall", "FIKTIV:Luftschicht 40", "<Unnamed>"),
            Wand("3ZE", 9000, 80, "IfcWall", "FIKTIV:Zementestrich 80", "<Unnamed>"));
        foreach (var (kz, x) in new[] { ("RO-1", 1000.0), ("RO-2", 4000), ("RO-3", 7000), ("RO-4", 10000) }) RohrY(p, kz, x);
        // alle Paare: die Luftschichten (Teil und Wand) erzeugen keine Kollision; der unbekannte Teil schon (P20 – offener Punkt B3c)
        var e1 = Ok(p.Ausfuehren("kollision_pruefen", Pruefen(p, KollisionTexte.Paare)));
        var kol = Assert.Single(Kollisionen(p).Where(k => k.Text("typ") == "durchdringung" && k.Text("rohbau") == "unbekannt"));
        Assert.EndsWith(":" + "3TX".PadRight(22, '0'), kol.Text("bauteil"));                // der unbekannte Teil
        Assert.DoesNotContain(Kollisionen(p), k => k.Text("material")?.StartsWith("Luftschicht") == true);
        Assert.Contains("2 Luftschicht(en) (in keinem Paar geprüft)", KollisionspruefungHilfe.Hinweis(e1, "Typenliste (0 Einträge)"));
        Ok(KollisionspruefungHilfe.Status(p, "bestaetigt", "FIKTIV: Luft?", kol.Kennzeichen));
        Assert.Equal(1, P20(p));

        // Christian legt den unbekannten Teil als Luftschicht fest: der nächste Lauf findet die Kollision nicht mehr → behoben
        Ok(Setzen(p, Fiktiv, ("FIKTIV Schicht X", "luftschicht")));
        var e2 = Ok(p.Ausfuehren("kollision_pruefen"));
        var nachher = Hole(p, kol.Kennzeichen);
        Assert.Equal("behoben", nachher.Text("status"));
        Assert.Contains("Luftschicht, wird nicht geprüft", nachher.Eigenschaft("status")!.Herkunft.Quelle);
        Assert.Contains("Davon 1 mit Luftschichten → behoben", KollisionspruefungHilfe.Hinweis(e2, "mit Luftschichten"));
        Assert.Contains("3 Luftschicht(en) (in keinem Paar geprüft)", KollisionspruefungHilfe.Hinweis(e2, "Typenliste (1 Einträge)"));
        Assert.Equal(0, P20(p));
        // keine Aussparung (die Wand-Luftschicht wäre aussparungsfähig)
        var v = Ok(Vorschlagen(p));
        Assert.Empty(Aussparungen(p));
        Assert.Contains("Luftschicht 1", v.Hinweise[0]);
        // Abfrage typmaterial: Luftschicht und Bodenaufbau aus dem Typnamen zum Bestätigen, mit Kategorie und Vermerk
        var o = (JsonObject)Tuer.Abfrage(p, "typmaterial");
        JsonNode Zeile(string s) => o["ausTypname"]!.AsArray().Single(z => z!["schluessel"]!.GetValue<string>() == s)!;
        var lh = Zeile("Luft, horizontal (Teil)");
        Assert.Equal(("Luftschicht", "luftschicht", false), (lh["gruppe"]!.GetValue<string>(), lh["kategorie"]!.GetValue<string>(), lh["aussparung"]!.GetValue<bool>()));
        Assert.Equal(Materialwortliste.EntscheidLuftschicht, lh["vermerk"]!.GetValue<string>());
        Assert.Null(lh["annahme"]);
        var ze = Zeile("Zementestrich 80");
        Assert.Equal("bodenaufbau", ze["kategorie"]!.GetValue<string>());
        Assert.Equal(Materialwortliste.AnnahmeBodenaufbau, ze["annahme"]!.GetValue<string>());
        Assert.Equal("luftschicht", o["typen"]![0]!["kategorie"]!.GetValue<string>());
        Assert.Contains(o["kategorien"]!.AsArray(), x => x!["kategorie"]!.GetValue<string>() == "luftschicht"
                                                        && x["wirkung"]!.GetValue<string>() == Typenliste.Wirkung("luftschicht"));
    }

    // ------------------------------------------------------------------ Hinweise von aussparungen_vorschlagen: Zahl je Grund vollständig

    [Fact]
    public void Vorschlag_ZahlJeGrundImmerVollstaendig_EinzelzeilenHoechstens100()
    {
        var p = Hintergrund(S.Quader("3W110", "IfcWall", [0, 0, 0], [40000, 110, 2600], materialien: ["Farbe, weiss"], typname: "DirectShape:" + T110));
        // 105 Rohre im Abstand 350 mm: Rand 350 − (55 + 2 × 20) > 100 mm – je eine Aussparung (Material unbekannt)
        for (int i = 0; i < 105; i++) RohrY(p, $"RO-{i + 1}", 200 + i * 350);
        var v1 = Ok(Vorschlagen(p));
        Assert.Equal(105, Aussparungen(p).Count);
        Assert.Contains("Liste je Grund (105 Zeile(n), vollständig; Einzelzeilen darunter höchstens 100):", v1.Hinweise);
        Assert.Contains("  neu – 105", v1.Hinweise);
        Assert.Equal(100, v1.Hinweise.Count(h => h.Contains(" neu: ")));
        Assert.Contains(v1.Hinweise, h => h.StartsWith("… und 5 weitere"));

        Ok(Setzen(p, Christian, (T110, "leichtbau")));
        var v2 = Ok(Vorschlagen(p));
        Assert.Empty(Aussparungen(p));
        Assert.Contains("  gelöscht: kein Beton bzw. Mauerwerk – 105", v2.Hinweise);
        Assert.Equal(100, v2.Hinweise.Count(h => h.Contains(" gelöscht: kein Beton bzw. Mauerwerk: ")));
        Assert.Contains(v2.Hinweise, h => h.StartsWith("… und 5 weitere"));
        // ohne Einzelzeilen keine Zusammenfassung
        var v3 = Ok(Vorschlagen(p));
        Assert.DoesNotContain(v3.Hinweise, h => h.StartsWith("Liste je Grund"));
    }

    // ------------------------------------------------------------------ Katalog, Texte

    [Fact]
    public void Katalog_KategorienBodenaufbauLuftschicht_TexteNennenTypenliste()
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        var auswahl = p.Katalog.Klasse(Typenliste.Klasse).Eigenschaften.Single(x => x.Name == "kategorie").Auswahl!;
        Assert.Equal(Typenliste.Kategorien.Keys, auswahl);
        Assert.Equal(["bodenaufbau", "luftschicht"], auswahl.TakeLast(2));
        Assert.Equal(Materialgruppe.Bodenaufbau, Typenliste.Kategorien["bodenaufbau"]);
        Assert.Equal(Materialgruppe.Luftschicht, Typenliste.Kategorien["luftschicht"]);
        Assert.Equal("bekleidung", Typenliste.KategorieVon(Materialgruppe.Unterdecke));       // «Unterdecken wie Bekleidung»
        Assert.Equal("bodenaufbau", Typenliste.KategorieVon(Materialgruppe.Bodenaufbau));
        Assert.Equal("luftschicht", Typenliste.KategorieVon(Materialgruppe.Luftschicht));
        Assert.Contains("hlks-bekleidung", Typenliste.Wirkung("bodenaufbau"));
        Assert.Equal("weder Kollision noch Aussparung noch P20 (in keinem Paar geprüft)", Typenliste.Wirkung("luftschicht"));
        Assert.All(Enum.GetValues<Materialgruppe>().Where(g => g is Materialgruppe.Unterdecke or Materialgruppe.Bodenaufbau or Materialgruppe.Luftschicht),
            g => Assert.False(Materialwortliste.Bauart(g)));
        Assert.Contains("bodenaufbau | luftschicht", TypmaterialTexte.ParTypen);
        Assert.Contains("luftschicht (weder Kollision noch Aussparung", TypmaterialTexte.Setzen);
        Assert.Contains("Typenliste des Projekts (typmaterial_setzen", AussparungTexte.Vorschlagen);
        Assert.Contains("Luftschichten nie", KollisionTexte.ParPaare);
        Assert.Contains("Luftschichten", KollisionTexte.Pruefen);
    }
}
