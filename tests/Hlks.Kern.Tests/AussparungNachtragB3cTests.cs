using System.Text.Json.Nodes;
using Xbim.Ifc4.Interfaces;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.AussparungHilfe;
using S = Hlks.Kern.Tests.NetzdateiTestschreiber;

namespace Hlks.Kern.Tests;

/// <summary>Nachtrag B3c (Entscheide Christian 19.10.2026): Aussparungen nur in Beton und Mauerwerk, durchgehend über Schichten direkt
/// hintereinander (Lücke ≤ 1 mm), Bekleidungen (IfcCovering) weder Kollision noch Aussparung (als Paar hlks-bekleidung zuschaltbar),
/// keine Abstandsprüfung. Hintergrund-Bauteile als Quader in einer Netzdatei Fassung 3 von Hand (Wände entlang x, Dicke in y), Rohre
/// der Testserie DN 50 (da 55, FIKTIV) entlang y auf z = 1000; Vorgaben FIKTIV: Spiel 20, Zusammenfassen 100 mm. Alle Materialnamen
/// FIKTIV. Handrechnungen in mm.</summary>
public sealed class AussparungNachtragB3cTests : IDisposable
{
    readonly List<Projekt> projekte = [];

    public void Dispose()
    {
        foreach (var p in projekte) try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Projekt mit einer IFC-Unterlage (B1b-Testdatei, nur Räume übernommen) und dieser Netzdatei als Hintergrund.</summary>
    Projekt Hintergrund(int fassung, params S.Satz[] saetze)
    {
        var p = IfcHilfe.Leer();
        projekte.Add(p);
        Ok(IfcHilfe.Uebernehmen(p, IfcNetzFassung2Testdatei.Datei("IFC4"), new JsonObject { ["bereiche"] = new JsonArray("raeume") }));
        S.AlsNetzDerUnterlage(p, fassung, saetze);
        Serie(p); Vorgaben(p);
        return p;
    }

    static S.Satz Wand(string gid, double x0, double y0, double y1, params string[] material) =>
        S.Quader(gid, "IfcWall", [x0, y0, 0], [x0 + 2000, y1, 2600], materialien: material);

    static string G(string gid) => gid.PadRight(22, '0')[..22];

    static void RohrY(Projekt p, string kz, double x) => Ok(Modul3Hilfe.Rohr(p, kz, "DN 50", [x, -500, 1000], [x, 1000, 1000]));

    static bool Unbekannt(Element a) => a.Eigenschaft("materialUnbekannt")?.Inhalt is JsonValue v && v.GetValue<bool>();

    // ------------------------------------------------------------------ Material

    [Fact]
    public void Material_BetonJa_GipsDaemmungHolzStahlNein_UnbekanntMitHinweis_Typname()
    {
        var p = Hintergrund(3,
            Wand("3BETON", 0, 0, 200, "Stahlbeton"),
            Wand("3GIPS", 3000, 0, 100, "Gipskarton"),
            Wand("3DAEMM", 6000, 0, 160, "Mineralwolle"),
            Wand("3HOLZ", 9000, 0, 160, "Brettschichtholz"),
            S.Quader("3STAHL", "IfcColumn", [12000, 0, 0], [12300, 300, 2600], materialien: ["Stahl S235"]),
            Wand("3UNBEK", 15000, 0, 200, "Material 42"),
            S.Quader("3TYP", "IfcWall", [18000, 0, 0], [20000, 200, 2600], materialien: ["Farbe, weiss"], typname: "Beton, Stahlbeton 250"),
            Wand("3OHNE", 21000, 0, 200));
        foreach (var (kz, x) in new[] { ("RO-1", 1000.0), ("RO-2", 4000), ("RO-3", 7000), ("RO-4", 10000), ("RO-5", 12150), ("RO-6", 16000), ("RO-7", 19000), ("RO-8", 22000) })
            RohrY(p, kz, x);
        var e = Ok(Vorschlagen(p));
        var alle = Aussparungen(p);
        // Beton, unbekannt, Beton nach Typname, ohne Material → Aussparung; Gips, Dämmung, Holz, Stahl → keine
        Assert.Equal(["RO-1", "RO-6", "RO-7", "RO-8"], alle.SelectMany(a => LeitungKz(p, a)).Order());
        var beton = VonLeitung(p, "RO-1");
        Assert.Equal("Beton: Stahlbeton", beton.Text("material"));
        Assert.False(Unbekannt(beton));
        Assert.Contains("ANNAHME", beton.Eigenschaft("material")!.Herkunft.Quelle);
        Assert.Equal("berechnung", beton.Eigenschaft("material")!.Herkunft.Art);
        Assert.Equal("unbekannt: Material 42", VonLeitung(p, "RO-6").Text("material"));
        Assert.True(Unbekannt(VonLeitung(p, "RO-6")));
        Assert.Equal("Beton: aus Typname «Beton, Stahlbeton 250»", VonLeitung(p, "RO-7").Text("material"));   // Nachtrag B3d: Herkunft sichtbar
        Assert.Equal("unbekannt: keine Materialzuordnung im IFC", VonLeitung(p, "RO-8").Text("material"));
        Assert.True(Unbekannt(VonLeitung(p, "RO-8")));
        var kopf = e.Hinweise[0];
        Assert.Contains("0 durchgehend über mehrere Bauteile, 2 mit unbekanntem Material", kopf);
        Assert.Contains("4 Durchgang/Durchgänge ohne Aussparung (kein Rohbau: Dämmung 1, Holz 1, Leichtbau 1, Stahl/Metall 1)", kopf);
        // Liste: Material und Kennzeichen
        var liste = (JsonObject)Tuer.Abfrage(p, "aussparungsliste");
        var z6 = liste["zeilen"]!.AsArray().Single(z => z!["leitungen"]![0]!.GetValue<string>() == "RO-6")!;
        Assert.Equal("unbekannt: Material 42", z6["material"]!.GetValue<string>());
        Assert.True(z6["materialUnbekannt"]!.GetValue<bool>());
        Assert.False(z6["durchgehend"]!.GetValue<bool>());
        Assert.Equal(2, liste["materialUnbekannt"]!.GetValue<int>());
        // zweiter Lauf: nichts ändert sich
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);
    }

    /// <summary>Referenzfall (Handrechnung): Beton y 0 … 150 und Kalksandstein y 150 … 275 direkt hintereinander (Lücke 0), Rohr DN 50
    /// (da 55) senkrecht bei x = 1000, z = 1000. Eine Aussparung über beide: Tiefe 150 + 125 = 275, Mitte y = 275 / 2 = 137.5,
    /// b × h = 55 + 2 × 20 = 95 → auf ganze cm 100 × 100 (Nachtrag B4e), UK/OK = 1000 ∓ 50; die Tiefe 275 bleibt (nicht gerundet).
    /// Haupt-Bauteil = das dickere (Beton 150 &gt; 125), bauteil2 = Kalksandstein.</summary>
    [Fact]
    public void ZweiSchichtenDirektHintereinander_EineDurchgehendeAussparung_TiefeUeberBeide_Referenz()
    {
        var p = Hintergrund(3, Wand("3SCHICHTA", 0, 0, 150, "Beton"), Wand("3SCHICHTB", 0, 150, 275, "Kalksandstein"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        RohrY(p, "RO-1", 1000);
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Etwa(275, a.Zahl("tiefe"), 1e-3, "tiefe");
        Etwa(100, a.Zahl("b"), 1e-6, "b"); Etwa(100, a.Zahl("h"), 1e-6, "h");
        Assert.Equal([1000, 137.5, 1000], Mitte(a));
        Etwa(950, a.Zahl("kote_uk"), 1e-6, "UK"); Etwa(1050, a.Zahl("kote_ok"), 1e-6, "OK");
        Assert.Equal([$"{ul.Id}:{G("3SCHICHTA")}", $"{ul.Id}:{G("3SCHICHTB")}"], Aussparungsbezug.BauteilSchluesselAlle(a));
        Assert.Equal(G("3SCHICHTA"), a.Kennungen!["bauteil"]);
        Assert.Equal(ul.Id, a.Bezuege["unterlage2"]);
        Assert.Equal(G("3SCHICHTB"), a.Kennungen["bauteil2"]);
        Assert.Equal("IfcWall", a.Kennungen["bauteilEntitaet2"]);
        Assert.Equal("Beton: Beton / Mauerwerk: Kalksandstein", a.Text("material"));
        Assert.Contains("Dicke über 2 Bauteile direkt hintereinander", a.Eigenschaft("tiefe")!.Herkunft.Quelle);
        Assert.Contains("1 durchgehend über mehrere Bauteile", e.Hinweise[0]);
        // zweiter Lauf stabil, Liste
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);
        var z = ((JsonObject)Tuer.Abfrage(p, "aussparungsliste"))["zeilen"]![0]!;
        Assert.True(z["durchgehend"]!.GetValue<bool>());
        Assert.Equal([$"{G("3SCHICHTA")} (UL-1)", $"{G("3SCHICHTB")} (UL-1)"], z["bauteile"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(G("3SCHICHTA"), z["bauteil"]!.GetValue<string>());
        // Kollisionsprüfung: die durchgehende Aussparung deckt beide Schichten – keine Durchdringung, kein P20
        var k = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("hlks-architektur") }));
        Assert.Empty(KollisionspruefungHilfe.Kollisionen(p));
        Assert.Contains("2 in Aussparung", KollisionspruefungHilfe.Hinweis(k, "Keine Kollision:"));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P20");
        // aussparung_form: dieselbe Kettenrechnung (Kreis um die Projektion + 2 × Spiel = 55 + 40 = 95 → ganze cm 100)
        Ok(Form(p, "rund", a.Kennzeichen));
        Etwa(100, Hole(p, a.Kennzeichen).Zahl("d"), 1e-6, "d");
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);   // die Kernbohrung von Hand bleibt, Bezüge unverändert
    }

    [Fact]
    public void BetonDaemmungMauerwerk_ZweiAussparungen_DaemmungOhne()
    {
        var p = Hintergrund(3, Wand("3BETON", 0, 0, 200, "Beton"), Wand("3DAEMM", 0, 200, 300, "Wärmedämmung"), Wand("3MAUER", 0, 300, 450, "Backstein"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        RohrY(p, "RO-1", 1000);
        var e = Ok(Vorschlagen(p));
        var alle = Aussparungen(p);
        Assert.Equal(2, alle.Count);
        var beton = alle.Single(a => a.Kennungen!["bauteil"] == G("3BETON"));
        var mauer = alle.Single(a => a.Kennungen!["bauteil"] == G("3MAUER"));
        Etwa(200, beton.Zahl("tiefe"), 1e-3); Assert.Equal([1000, 100, 1000], Mitte(beton));
        Etwa(150, mauer.Zahl("tiefe"), 1e-3); Assert.Equal([1000, 375, 1000], Mitte(mauer));
        Assert.All(alle, a => Assert.Single(Aussparungsbezug.BauteilSchluesselAlle(a)));
        Assert.Equal("Mauerwerk: Backstein", mauer.Text("material"));
        Assert.Contains("(kein Rohbau: Dämmung 1)", e.Hinweise[0]);
        Assert.DoesNotContain(alle, a => Aussparungsbezug.BauteilSchluesselAlle(a).Contains($"{ul.Id}:{G("3DAEMM")}"));
    }

    /// <summary>«Direkt» = Lücke ≤ 1 mm: 0.8 mm → eine Aussparung (Tiefe 300 inkl. Spalt), 2 mm → zwei.</summary>
    [Fact]
    public void Luecke_BisEinMillimeterDurchgehend_DarueberGetrennt()
    {
        var p = Hintergrund(3,
            Wand("3NAHA", 0, 0, 150, "Beton"), Wand("3NAHB", 0, 150.8, 300, "Beton"),
            Wand("3WEITA", 3000, 0, 150, "Beton"), Wand("3WEITB", 3000, 152, 300, "Beton"));
        RohrY(p, "RO-1", 1000);
        RohrY(p, "RO-2", 4000);
        Ok(Vorschlagen(p));
        var nah = Assert.Single(Aussparungen(p), a => LeitungKz(p, a).Contains("RO-1"));
        Assert.Equal(2, Aussparungsbezug.BauteilSchluesselAlle(nah).Count);
        Etwa(300, nah.Zahl("tiefe"), 1e-3);
        var weit = Aussparungen(p).Where(a => LeitungKz(p, a).Contains("RO-2")).ToList();
        Assert.Equal(2, weit.Count);
        Assert.All(weit, a => Assert.Single(Aussparungsbezug.BauteilSchluesselAlle(a)));
        Assert.Equal(1.0, Aussparungsplanung.Kettenluecke);
    }

    [Fact]
    public void NetzdateiFassung2_OhneMaterial_VorschlagMitHinweis()
    {
        var p = Hintergrund(2, Wand("3ALT", 0, 0, 200));
        RohrY(p, "RO-1", 1000);
        var e = Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.True(Unbekannt(a));
        Assert.Contains("Netzdatei Fassung 2 ohne Material", a.Text("material"));
        Assert.Contains("1 mit unbekanntem Material", e.Hinweise[0]);
    }

    // ------------------------------------------------------------------ KICAD-Bauteile

    [Fact]
    public void Kicad_MaterialDerWand_LeichtbauLoeschtVorschlag_FesteBleibt_KetteZweierWaende()
    {
        var p = Rechteck("beton");
        projekte.Add(p);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Geschoss", ["kennzeichen"] = "OG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 3000, ["hoehe"] = 3000 },
        }));
        Serie(p); Vorgaben(p);
        RohrDurchW1(p, "RO-1", 1000);
        RohrDurchW1(p, "RO-2", 3000);
        Ok(Vorschlagen(p));
        Assert.Equal("Beton: beton", VonLeitung(p, "RO-1").Text("material"));
        Assert.False(Unbekannt(VonLeitung(p, "RO-1")));
        var fest = VonLeitung(p, "RO-2");
        Ok(Status(p, "freigegeben", fest.Kennzeichen));
        // W-1 wird Leichtbau: der Vorschlag wird gelöscht, die freigegebene bleibt (abgegeben), mit Hinweis
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-1", ["eigenschaften"] = new JsonObject { ["material"] = "leichtbau" } }));
        var e = Ok(Vorschlagen(p));
        Assert.Single(Aussparungen(p));
        Assert.Equal(AussparungStatus.Freigegeben, AussparungStatus.Von(Hole(p, fest.Kennzeichen)));
        Assert.Contains(e.Hinweise, h => h.Contains("gelöscht: kein Beton bzw. Mauerwerk") && h.Contains("W-1"));
        Assert.Contains(e.Hinweise, h => h.StartsWith(fest.Kennzeichen) && h.Contains("bleibt, weil schon abgegeben"));
        Assert.Contains("1 Vorschlag/Vorschläge darin gelöscht, 1 feste darin bleiben (abgegeben)", e.Hinweise[0]);
        // wieder Beton; zweite Wand W-5 (Kalksandstein) direkt vor W-1: W-1 y −100 … 100, W-5 y −250 … −100
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "W-1", ["eigenschaften"] = new JsonObject { ["material"] = "beton" } }));
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Wand", ["kennzeichen"] = "W-5", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["material"] = "kalksandstein" },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([500, -175, 0], [2500, -175, 0]), ["dicke"] = 150, ["hoehe"] = 3000 },
        }));
        Ok(Vorschlagen(p));
        // Handrechnung: Tiefe 150 + 200 = 350, Mitte y = (−250 + 100) / 2 = −75; Haupt-Bauteil W-1 (200 > 150), bauteil2 W-5
        var a = VonLeitung(p, "RO-1");
        Etwa(350, a.Zahl("tiefe"), 1e-3, "tiefe");
        Assert.Equal([1000, -75, 1000], Mitte(a));
        Assert.Equal(Hole(p, "W-1").Id, a.Bezuege["bauteil"]);
        Assert.Equal(Hole(p, "W-5").Id, a.Bezuege["bauteil2"]);
        Assert.Equal("Mauerwerk: kalksandstein / Beton: beton", a.Text("material"));   // Folge entlang der Achse (+y)
        Assert.Empty(Ok(Vorschlagen(p)).Aenderungen);
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P08" && b.Element.StartsWith("AUS-"));
        // W-5 löschen: Bezug gelöst, veraltet; der nächste Lauf schlägt die Aussparung in W-1 allein vor
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "W-5" }));
        Assert.Equal(AussparungStatus.Veraltet, AussparungStatus.Von(Hole(p, a.Kennzeichen)));
        Assert.False(Hole(p, a.Kennzeichen).Bezuege.ContainsKey("bauteil2"));
        Ok(Vorschlagen(p));
        var neu = VonLeitung(p, "RO-1");
        Etwa(200, neu.Zahl("tiefe"), 1e-3);
        Assert.Single(Aussparungsbezug.BauteilSchluesselAlle(neu));
    }

    // ------------------------------------------------------------------ Bekleidungen und Kollisionen

    [Fact]
    public void Bekleidung_KeineKollision_MitPaarGemeldet_NieAussparung_AlteBleibenUnveraendert()
    {
        var p = Hintergrund(3,
            S.Quader("3BEKL", "IfcCovering", [0, -20, 0], [2000, 0, 2600], materialien: ["Gipsputz"]),
            Wand("3WAND", 0, 0, 200, "Beton"),
            Wand("3GIPS", 3000, 0, 100, "Gipskarton"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        RohrY(p, "RO-1", 1000);
        RohrY(p, "RO-2", 4000);
        // Vorgabe: keine Kollision mit der Bekleidung; Durchdringungen mit Material
        var e1 = Ok(p.Ausfuehren("kollision_pruefen"));
        var ks = KollisionspruefungHilfe.Kollisionen(p);
        Assert.Equal(2, ks.Count);
        Assert.DoesNotContain(ks, k => KollisionspruefungHilfe.Beteiligte(k).Contains($"{ul.Id}:{G("3BEKL")}"));
        var beton = ks.Single(k => k.Text("bauteil") == $"{ul.Id}:{G("3WAND")}");
        var gips = ks.Single(k => k.Text("bauteil") == $"{ul.Id}:{G("3GIPS")}");
        Assert.Equal(("ja", "Beton: Beton"), (beton.Text("rohbau"), beton.Text("material")));
        Assert.Equal(("nein", "Leichtbau: Gipskarton"), (gips.Text("rohbau"), gips.Text("material")));
        // P20 nur für das Bauteil, das eine Aussparung braucht (Beton), nicht für Gips
        var p20 = Pruefungen.Alle(p.Modell, p.Katalog).Where(b => b.Code == "P20").ToList();
        Assert.Equal(["RO-1"], p20.Select(b => b.Element));
        // mit Paar hlks-bekleidung: die Bekleidung wird gemeldet (Kontakt)
        var e2 = Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("hlks-architektur", "hlks-bekleidung") }));
        var bek = Assert.Single(KollisionspruefungHilfe.Kollisionen(p), k => KollisionspruefungHilfe.Beteiligte(k).Contains($"{ul.Id}:{G("3BEKL")}"));
        Assert.Equal(("hart", "kontakt"), (bek.Text("art"), bek.Text("typ")));
        Assert.Contains("hlks-bekleidung: ", KollisionspruefungHilfe.Hinweis(e2, "  hlks-bekleidung"));
        Ok(KollisionspruefungHilfe.Status(p, "bestaetigt", "FIKTIV: Putz anpassen", bek.Kennzeichen));
        // wieder mit der Vorgabe: die Kollision mit der Bekleidung bleibt unverändert (ausserhalb dieses Laufs), wird nicht behoben
        var e3 = Ok(p.Ausfuehren("kollision_pruefen"));
        var nachher = Hole(p, bek.Kennzeichen);
        Assert.Equal("bestaetigt", nachher.Text("status"));
        Assert.DoesNotContain("nicht mehr gefunden", nachher.Text("pruefung"));
        Assert.Contains("1 ausserhalb dieses Laufs (unverändert)", KollisionspruefungHilfe.Hinweis(e3, "Status:"));
        Assert.Contains("Davon 1 mit Bekleidungen", KollisionspruefungHilfe.Hinweis(e3, "Bekleidungen (IfcCovering)"));
        // Kollisionsliste: Paar je Zeile, Filter
        var liste = (JsonObject)Tuer.Abfrage(p, "kollisionen");
        Assert.Equal(2, liste["zusammenfassung"]!["jePaar"]!["hlks-architektur"]!.GetValue<int>());
        Assert.Equal(1, liste["zusammenfassung"]!["jePaar"]!["hlks-bekleidung"]!.GetValue<int>());
        var nurBek = (JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["paar"] = "hlks-bekleidung" });
        Assert.Equal(bek.Kennzeichen, Assert.Single(nurBek["zeilen"]!.AsArray())!["kollision"]!.GetValue<string>());
        Assert.Equal("Leichtbau: Gipskarton", liste["zeilen"]!.AsArray().Single(z => z!["kollision"]!.GetValue<string>() == gips.Kennzeichen)!["material"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "kollisionen", new JsonObject { ["paar"] = "hlks-mond" }));
        // Aussparungen: nur in der Betonwand – nie in der Bekleidung, nicht im Gips
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Assert.Equal([$"{ul.Id}:{G("3WAND")}"], Aussparungsbezug.BauteilSchluesselAlle(a));
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), b => b.Code == "P20");
    }

    [Fact]
    public void Paare_BekleidungNichtInDerVorgabe_ArchitekturHlksUmgedrehtGeht()
    {
        Assert.Equal(["hlks-architektur", "hlks-hlks"], KollisionTexte.PaareVorgabe);
        Assert.Contains("hlks-bekleidung", KollisionTexte.Paare);
        Assert.Contains("hlks-bekleidung", KollisionTexte.ParPaare);
        var p = Hintergrund(3, S.Quader("3BEKL", "IfcCovering", [0, -20, 0], [2000, 0, 2600]));
        RohrY(p, "RO-1", 1000);
        Ok(p.Ausfuehren("kollision_pruefen", new JsonObject { ["paare"] = new JsonArray("bekleidung-hlks") }));
        Assert.Contains(" · hlks-bekleidung", Assert.Single(KollisionspruefungHilfe.Kollisionen(p)).Text("pruefung"));
    }

    // ------------------------------------------------------------------ Abstandsprüfung, Vorgaben

    [Fact]
    public void Vorgaben_AbstaendeOptional_KeinHinweisBeiJedemLauf_GesetztWeichWieBisher()
    {
        var p = Hintergrund(3, Wand("3WAND", 0, 0, 200, "Beton"));
        var o = (JsonObject)Tuer.Abfrage(p, "vorgaben");
        var fehlend = o["fehlend"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()).ToList();
        Assert.DoesNotContain("abstand_weich", fehlend);
        Assert.DoesNotContain("abstand_leitungen", fehlend);
        var optional = o["optional"]!.AsArray();
        Assert.Equal(["abstand_weich", "abstand_leitungen"], optional.Select(x => x!["name"]!.GetValue<string>()));
        Assert.All(optional, x =>
        {
            Assert.Equal("Abstandsprüfung aus (Entscheid Christian 19.10.2026); setzbar, falls gewünscht", x!["hinweis"]!.GetValue<string>());
            Assert.False(x["gesetzt"]!.GetValue<bool>());
        });
        Assert.StartsWith("keine – Abstandsprüfung aus", Projektvorgaben.Wirkung[Projektvorgaben.AbstandLeitungen]);
        // zwei Rohre 30 mm auseinander (FIKTIV): kein weicher Befund, keine Meldung «Weiche Prüfung …», nur knapp «Abstandsprüfung aus»
        Ok(Modul3Hilfe.Rohr(p, "RO-1", "DN 20", [3000, 0, 1000], [6000, 0, 1000]));
        Ok(Modul3Hilfe.Rohr(p, "RO-2", "DN 20", [3000, 55, 1000], [6000, 55, 1000]));
        var e = Ok(p.Ausfuehren("kollision_pruefen"));
        Assert.Empty(KollisionspruefungHilfe.Kollisionen(p));
        Assert.DoesNotContain(e.Hinweise, h => h.Contains("Weiche Prüfung") || h.Contains("abstand_leitungen"));
        Assert.Single(e.Hinweise, h => h.Contains("Abstandsprüfung aus"));
        // gesetzt (FIKTIV 50 mm): weiche Prüfung wie bisher, Abfrage nennt den Wert als optional gesetzt
        Ok(KollisionspruefungHilfe.Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 50)));
        var e2 = Ok(p.Ausfuehren("kollision_pruefen"));
        Assert.Equal("weich", Assert.Single(KollisionspruefungHilfe.Kollisionen(p)).Text("art"));
        Assert.DoesNotContain(e2.Hinweise, h => h.Contains("Abstandsprüfung aus"));
        var o2 = (JsonObject)Tuer.Abfrage(p, "vorgaben");
        Assert.True(o2["werte"]!["abstand_leitungen"]!["optional"]!.GetValue<bool>());
        Assert.True(o2["optional"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "abstand_leitungen")!["gesetzt"]!.GetValue<bool>());
    }

    // ------------------------------------------------------------------ IFC-Ausgabe, Abgleich

    [Fact]
    public void IfcAusgabe_DurchgehendeAussparung_BauteilUndWeitereUndMaterialImPset()
    {
        var p = Hintergrund(3, Wand("3SCHICHTA", 0, 0, 150, "Beton"), Wand("3SCHICHTB", 0, 150, 275, "Kalksandstein"));
        RohrY(p, "RO-1", 1000);
        Ok(Vorschlagen(p));
        var a = Assert.Single(Aussparungen(p));
        Ok(Status(p, "freigegeben", a.Kennzeichen));
        var pfad = Path.Combine(Unterlagen.Ordner(p), "b3c_aussparungen.ifc");
        var bericht = (JsonObject)Tuer.Abfrage(p, "aussparungen_ifc", new JsonObject { ["datei"] = pfad, ["fassung"] = "IFC4" });
        Assert.NotNull(bericht);
        using var d = IfcDatei.Oeffnen(pfad);
        var gid = AussparungIfcExport.AussparungGlobalId(p.Kopf.Id, a.Id);
        var px = d.Modell.Instances.OfType<IIfcBuildingElementProxy>().Single(x => x.GlobalId.ToString() == gid);
        var eig = d.Eigenschaften(px);
        string T(string n) => eig[$"{AussparungIfcExport.PsetAussparung}.{n}"].Inhalt!.ToString()!;
        Assert.Equal(G("3SCHICHTA"), T("Bauteil"));
        Assert.Equal(G("3SCHICHTB"), T("BauteileWeitere"));
        Assert.Equal("Beton: Beton / Mauerwerk: Kalksandstein", T("Material"));
        Assert.Equal("nein", T("MaterialUnbekannt"));
        Assert.Equal(275, d.LaengeMm(eig[$"{AussparungIfcExport.PsetAussparung}.Tiefe"])!.Value, 0.1);
    }

    /// <summary>Abgleich einer durchgehenden Aussparung: je Bauteil zählen nur die Punkte des Aussparungskörpers in seinem Netz
    /// (AbgedecktIn). Öffnung nur in der ersten Schicht → diese gedeckt, die zweite nicht.</summary>
    [Fact]
    public void Abgleich_DurchgehendeAussparung_JeBauteilNurSeinTeil()
    {
        var lage = new Aussparungslage(new V3(1000, 137.5, 1000), new V3(0, 1, 0), new V3(1, 0, 0), new V3(0, 0, 1), false, 95, 95, 275);
        var a = Pruefnetz.Aus(NetzBau.Quader([0, 0, 0], [2000, 150, 2600]).Ecken.SelectMany(q => q.Select(x => (float)x)).ToArray(),
            NetzBau.Quader([0, 0, 0], [2000, 150, 2600]).Dreiecke.SelectMany(x => x).ToArray());
        var b = Pruefnetz.Aus(NetzBau.Quader([0, 150, 0], [2000, 275, 2600]).Ecken.SelectMany(q => q.Select(x => (float)x)).ToArray(),
            NetzBau.Quader([0, 150, 0], [2000, 275, 2600]).Dreiecke.SelectMany(x => x).ToArray());
        var o = NetzBau.Quader([900, -10, 900], [1100, 160, 1100]);
        var oeffnung = new Pruefoeffnung
        {
            UnterlageId = "UL", GlobalId = "O", Entitaet = "IfcOpeningElement",
            Netz = Pruefnetz.Aus(o.Ecken.SelectMany(q => q.Select(x => (float)x)).ToArray(), o.Dreiecke.SelectMany(x => x).ToArray()),
        };
        Assert.True(AussparungenAbgleichen.AbgedecktIn(oeffnung, lage, a));
        Assert.False(AussparungenAbgleichen.AbgedecktIn(oeffnung, lage, b));
        Assert.False(AussparungenAbgleichen.Abgedeckt(oeffnung, lage));   // ohne Aufteilung deckt sie die ganze Tiefe nicht
    }
}
