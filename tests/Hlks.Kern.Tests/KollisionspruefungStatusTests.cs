using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;
using static Hlks.Kern.Tests.Modul3Hilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2: Kollisionen als Elemente über Prüfläufe – Statuslogik (neu → bleibt → behoben → wieder neu, akzeptiert
/// bleibt), stabile Kennung (kleine Verschiebung &lt; 50 mm dieselbe, grosse eine neue), kollision_status, Trockenlauf und
/// Rückgängig, Kollisionsliste. B2b: pruefung = Lauf, der die Kollision zuerst fand (ergänzt nur bei Statuswechsel durch einen Lauf),
/// Zähler in Projektinformation.kollisionslauf, ein Lauf ohne Änderung schreibt nur die Projektinformation. Masse FIKTIV (Testrohrserie).</summary>
public class KollisionspruefungStatusTests
{
    [Fact]
    public void Statuslogik_UeberVierLaeufe_NeuBleibtBehobenWiederNeu_AkzeptiertBleibt()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [2000, -500, 1000], [2000, 500, 1000]));
        Ok(Rohr(p, "RO-2", "DN 20", [4000, -500, 1000], [4000, 500, 1000]));
        const string Zeit = @"\d{4}-\d{2}-\d{2} \d{2}:\d{2}";
        const string Lauf1 = $"^{Zeit} · Lauf 1 · hlks-architektur, hlks-hlks";
        // Lauf 1: beide neu; pruefung = Lauf 1, Zähler in der (neu angelegten) Projektinformation
        Ok(Pruefen(p));
        Assert.Equal(["RO-1 ↔ W-1", "RO-2 ↔ W-1"], Kollisionen(p).Select(k => k.Name));
        Assert.All(Kollisionen(p), k => Assert.Equal("neu", k.Text("status")));
        Assert.All(Kollisionen(p), k => Assert.Matches(Lauf1 + "$", k.Text("pruefung")));
        Assert.Matches($"^{Zeit} · Lauf 1 · hlks-architektur, hlks-hlks$", Kollisionslauf(p));
        Ok(Status(p, "bestaetigt", "Architekt informiert", "KOL-1"));
        Ok(Status(p, "akzeptiert", null, "KOL-2"));
        Assert.Equal("mensch", Hole(p, "KOL-1").Eigenschaft("status")!.Herkunft.Art);
        var stand1 = Kollisionen(p).ToDictionary(k => k.Kennzeichen, k => (k.Version, Json.Text(k)));
        // Lauf 2 ohne Änderung: Status bleiben, Kollisionen nicht neu geschrieben – pruefung nennt weiter Lauf 1, der Zähler Lauf 2
        var l2 = Ok(Pruefen(p));
        Assert.Equal("bestaetigt", Hole(p, "KOL-1").Text("status"));
        Assert.Equal("Architekt informiert", Hole(p, "KOL-1").Text("kommentar"));
        Assert.Equal("akzeptiert", Hole(p, "KOL-2").Text("status"));
        Assert.Matches(Lauf1 + "$", Hole(p, "KOL-1").Text("pruefung"));
        Assert.DoesNotContain("Lauf 2", Hole(p, "KOL-1").Text("pruefung"));
        Assert.Matches($"^{Zeit} · Lauf 2 · hlks-architektur, hlks-hlks$", Kollisionslauf(p));
        Assert.Equal(stand1, Kollisionen(p).ToDictionary(k => k.Kennzeichen, k => (k.Version, Json.Text(k))));
        Assert.Equal("PI-1", Assert.Single(Ops(p, l2)).Element!.Kennzeichen);
        Assert.Contains("0 neu, 2 wieder gefunden", Hinweis(l2, "Status:"));
        Assert.Contains("0 Kollision(en) neu oder geändert, 2 unverändert", Hinweis(l2, "Geschrieben:"));
        Assert.Equal(2, Kollisionen(p).Count);
        // Lauf 3: beide Rohre aus der Wand – KOL-1 behoben (nicht gelöscht), pruefung ergänzt; KOL-2 akzeptiert bleibt (nicht geschrieben)
        Geometrie(p, "RO-1", [2000, 300, 1000], [2000, 900, 1000]);
        Geometrie(p, "RO-2", [4000, 300, 1000], [4000, 900, 1000]);
        var l3 = Ok(Pruefen(p));
        Assert.Equal("behoben", Hole(p, "KOL-1").Text("status"));
        Assert.Equal("berechnung", Hole(p, "KOL-1").Eigenschaft("status")!.Herkunft.Art);
        Assert.Matches($"{Lauf1} · nicht mehr gefunden in Lauf 3 \\({Zeit}\\)$", Hole(p, "KOL-1").Text("pruefung"));
        Assert.Equal("akzeptiert", Hole(p, "KOL-2").Text("status"));
        Assert.Equal(stand1["KOL-2"], (Hole(p, "KOL-2").Version, Json.Text(Hole(p, "KOL-2"))));
        Assert.Equal(["KOL-1", "PI-1"], Ops(p, l3).Select(o => o.Element!.Kennzeichen).Order());
        Assert.Contains("Lauf 3", Kollisionslauf(p));
        Assert.Contains("1 nicht mehr gefunden → behoben, 1 akzeptiert und nicht mehr gefunden (bleibt)", Hinweis(l3, "Status:"));
        // Lauf 4: RO-1 wieder durch die Wand → KOL-1 wieder neu (gleiche Kennung), Hinweis, pruefung ergänzt
        Geometrie(p, "RO-1", [2000, -500, 1000], [2000, 500, 1000]);
        var l4 = Ok(Pruefen(p));
        Assert.Equal("neu", Hole(p, "KOL-1").Text("status"));
        Assert.Contains("KOL-1", Hinweis(l4, "Wieder aufgetreten"));
        Assert.Matches($"{Lauf1} · nicht mehr gefunden in Lauf 3 \\({Zeit}\\) · wieder gefunden in Lauf 4 \\({Zeit}\\)$", Hole(p, "KOL-1").Text("pruefung"));
        Assert.Equal("akzeptiert", Hole(p, "KOL-2").Text("status"));
        Assert.Equal(["KOL-1", "PI-1"], Ops(p, l4).Select(o => o.Element!.Kennzeichen).Order());
        Assert.Contains("Lauf 4", Kollisionslauf(p));
        Assert.Equal(2, Kollisionen(p).Count);                                // nie gelöscht, keine Doppel
        Assert.Single(p.Modell.DerKlasse("Projektinformation"));             // einmal angelegt, danach nur ergänzt
    }

    [Fact]
    public void ZweiterLauf_OhneAenderung_SchreibtNurDieProjektinformation()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));    // durchdringung W-1
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 112, 1000], [2000, 112, 1000]));     // 0.5 mm an W-1: kontakt
        Ok(Rohr(p, "RO-3", "DN 20", [1000, 2000, 1000], [5000, 2000, 1000]));   // HLKS gegen HLKS: kreuzt RO-4
        Ok(Rohr(p, "RO-4", "DN 20", [3500, 1000, 1000], [3500, 3000, 1000]));
        var l1 = Ok(Pruefen(p));
        int n = Kollisionen(p).Count;
        Assert.True(n >= 3, $"{n} Kollisionen");
        Assert.Equal(n + 1, Ops(p, l1).Count);                                // alle Kollisionen + Projektinformation (angelegt)
        Assert.Contains(l1.Aenderungen, a => a.Art == "angelegt" && a.Klasse == "Projektinformation");
        var stand = Kollisionen(p).ToDictionary(k => k.Id, k => (k.Version, Json.Text(k)));
        // zweiter Lauf: genau eine Operation – die Projektinformation; keine Kollision geändert
        var l2 = Ok(Pruefen(p));
        var op = Assert.Single(Ops(p, l2));
        Assert.Equal(("element.setzen", "Projektinformation"), (op.Art, op.Element!.Klasse));
        var a2 = Assert.Single(l2.Aenderungen);
        Assert.Equal(("geaendert", "Projektinformation"), (a2.Art, a2.Klasse));
        Assert.Equal(stand, Kollisionen(p).ToDictionary(k => k.Id, k => (k.Version, Json.Text(k))));
        Assert.Contains($"0 Kollision(en) neu oder geändert, {n} unverändert", Hinweis(l2, "Geschrieben:"));
        Assert.Contains("Lauf 2", Kollisionslauf(p));
        Assert.Equal("berechnung", HuelleHilfe.Projektinfo(p.Modell)!.Eigenschaft("kollisionslauf")!.Herkunft.Art);
        Assert.Equal("kollision_pruefen, Lauf 2", HuelleHilfe.Projektinfo(p.Modell)!.Eigenschaft("kollisionslauf")!.Herkunft.Quelle);
        // Trockenlauf danach zeigt ebenfalls nur die Projektinformation
        var tl = Ok(Pruefen(p, trocken: true));
        Assert.Equal("Projektinformation", Assert.Single(tl.Aenderungen).Klasse);
        // Daten geändert ohne Statuswechsel (Rohr 30 mm verschoben, gleiche Kollision): geschrieben, pruefung bleibt Lauf 1
        Geometrie(p, "RO-1", [3030, -500, 1000], [3030, 500, 1000]);
        var l3 = Ok(Pruefen(p));
        var kol = Kollisionen(p).Single(k => k.Name == "RO-1 ↔ W-1");
        Assert.Equal([3030, 0, 1000], Lage(kol));
        Assert.Contains(" · Lauf 1 · ", kol.Text("pruefung"));
        Assert.DoesNotContain("Lauf 3", kol.Text("pruefung"));
        Assert.Equal([kol.Kennzeichen, "PI-1"], Ops(p, l3).Select(o => o.Element!.Kennzeichen).Order());
        Assert.Contains("Lauf 3", Kollisionslauf(p));
    }

    [Fact]
    public void LaufNummer_ZaehltWeiter_OhneKollisionen_UndAusAeltererPruefung()
    {
        var p = Wandprojekt();
        // vorhandene Projektinformation wird ergänzt, nicht ersetzt
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI-1", ["eigenschaften"] = new JsonObject { ["phase"] = "Bauprojekt" } }));
        // Lauf 1 und 2 ohne Kollision: kein Kollisionselement, aber der Zähler läuft
        var l1 = Ok(Pruefen(p));
        Assert.Empty(Kollisionen(p));
        Assert.Contains("Lauf 1", Kollisionslauf(p));
        Assert.Equal("PI-1", Assert.Single(Ops(p, l1)).Element!.Kennzeichen);
        Ok(Pruefen(p));
        Assert.Contains("Lauf 2", Kollisionslauf(p));
        Assert.Equal("Bauprojekt", Hole(p, "PI-1").Text("phase"));
        Assert.Single(p.Modell.DerKlasse("Projektinformation"));
        // Lauf 3 findet die erste Kollision: sie trägt Lauf 3
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        var l3 = Ok(Pruefen(p));
        Assert.Contains("Prüflauf 3 ", Hinweis(l3, "Prüflauf"));
        Assert.Contains(" · Lauf 3 · ", Hole(p, "KOL-1").Text("pruefung"));
        // ältere Projekte: grösste Nummer aus kollisionslauf UND den pruefung-Texten (auch «nicht mehr gefunden in Lauf n»)
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["pruefung"] = "2026-10-01 08:00 · Lauf 7 · hlks-architektur · nicht mehr gefunden in Lauf 9 (2026-10-02 08:00)" } }));
        Assert.Equal(10, KollisionPruefen.NaechsterLauf(p.Modell, p.Katalog));
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "PI-1", ["eigenschaften"] = new JsonObject { ["kollisionslauf"] = "2026-10-03 08:00 · Lauf 12 · hlks-hlks" } }));
        Assert.Equal(13, KollisionPruefen.NaechsterLauf(p.Modell, p.Katalog));
        var l4 = Ok(Pruefen(p));
        Assert.Contains("Prüflauf 13 ", Hinweis(l4, "Prüflauf"));
        Assert.Contains("Lauf 13", Kollisionslauf(p));
        Assert.StartsWith("2026-10-01 08:00 · Lauf 7", Hole(p, "KOL-1").Text("pruefung"));   // wieder gefunden, Status bleibt: unverändert
        // ohne Projektinformation und Kollisionen: Lauf 1
        Assert.Equal(1, KollisionPruefen.NaechsterLauf(new Modell(), p.Katalog));
    }

    static string Kollisionslauf(Projekt p) =>
        HuelleHilfe.Projektinfo(p.Modell)?.Text("kollisionslauf") ?? throw new Xunit.Sdk.XunitException("Projektinformation.kollisionslauf fehlt");

    /// <summary>Operationen des Pakets eines Befehls.</summary>
    static List<Operation> Ops(Projekt p, BefehlsErgebnis e) => p.Verlauf.Single(x => x.Id == e.PaketId).Operationen;

    [Fact]
    public void StabileKennung_KleineVerschiebung_Dieselbe_GrosseNeu()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3040, -500, 1000], [3040, 500, 1000]));
        Ok(Pruefen(p));
        var k1 = Assert.Single(Kollisionen(p));
        var kennung = k1.Text("kennung")!;
        Assert.EndsWith("@3000,0,1000", kennung);                             // 3040 → 3000 (100-mm-Raster)
        // 30 mm über die Rastergrenze (3070 → 3100): dieselbe Kollision, Kennung bleibt, Lage nachgeführt
        Geometrie(p, "RO-1", [3070, -500, 1000], [3070, 500, 1000]);
        Ok(Pruefen(p));
        var k2 = Assert.Single(Kollisionen(p));
        Assert.Equal((k1.Id, kennung), (k2.Id, k2.Text("kennung")));
        Assert.Equal([3070, 0, 1000], Lage(k2));
        Assert.Equal("neu", k2.Text("status"));
        Assert.Equal(k1.Text("pruefung"), k2.Text("pruefung"));               // Daten nachgeführt, pruefung bleibt (zuerst gefunden)
        // weitere 40 mm zurück (3030): noch immer dieselbe
        Ok(Status(p, "bestaetigt", null, "KOL-1"));
        Geometrie(p, "RO-1", [3030, -500, 1000], [3030, 500, 1000]);
        Ok(Pruefen(p));
        Assert.Equal("bestaetigt", Assert.Single(Kollisionen(p)).Text("status"));
        // grosse Verschiebung (570 mm): neue Kollision mit neuer Kennung, die alte behoben
        Geometrie(p, "RO-1", [3600, -500, 1000], [3600, 500, 1000]);
        Ok(Pruefen(p));
        var ks = Kollisionen(p);
        Assert.Equal(2, ks.Count);
        Assert.Equal("behoben", Hole(p, "KOL-1").Text("status"));
        Assert.Equal("neu", Hole(p, "KOL-2").Text("status"));
        Assert.EndsWith("@3600,0,1000", Hole(p, "KOL-2").Text("kennung"));
        Assert.Equal(Kollisionskennung.Bilden(Beteiligte(Hole(p, "KOL-2")), new V3(3600, 0, 1000)), Hole(p, "KOL-2").Text("kennung"));
    }

    [Fact]
    public void Trockenlauf_ZahlenOhneAenderung_Rueckgaengig_StelltHer()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        var start = p.Modell.Pruefsumme();
        var tl = Ok(Pruefen(p, trocken: true));
        Assert.True(tl.Trockenlauf);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Empty(Kollisionen(p));
        Assert.Contains("1 Kollisionen – hart 1 (durchdringung 1, kontakt 0), weich 0", Hinweis(tl, "Prüflauf 1"));
        Assert.Contains("angelegt", tl.Aenderungen.Select(a => a.Art));
        Assert.Contains("KOL-1", tl.Aenderungen.Select(a => a.Kennzeichen));
        Ok(Pruefen(p));
        Assert.NotEqual(start, p.Modell.Pruefsumme());
        Ok(p.Rueckgaengig());
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.Empty(Kollisionen(p));
    }

    [Fact]
    public void KollisionStatus_KommentarErsetztEigenenText_BcfBloeckeBleiben_Ablehnungen()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        Ok(Pruefen(p));
        // Kommentar mit einem aus BCF übernommenen Block (Format BcfKommentare)
        var bcf = BcfKommentare.Block(new BcfKommentar(Guid.Parse("11111111-2222-3333-4444-555555555555"), new DateTimeOffset(2026, 10, 19, 10, 0, 0, TimeSpan.Zero), "arch@example.ch", "Wird verschoben"), DateTimeOffset.Now);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KOL-1", ["eigenschaften"] = new JsonObject { ["kommentar"] = "alt\n" + bcf } }));
        var e = Ok(Status(p, "erledigt", "Leitung umgelegt", "KOL-1", "KOL-1"));
        var k = Hole(p, "KOL-1");
        Assert.Equal("erledigt", k.Text("status"));
        Assert.Equal("Leitung umgelegt\n" + bcf, k.Text("kommentar"));
        Assert.Contains("Status neu → erledigt", Hinweis(e, "KOL-1:"));
        Assert.Contains("Keine Änderung.", Ok(Status(p, "erledigt", null, "KOL-1")).Hinweise);
        Nein(Status(p, "vielleicht", null, "KOL-1"));
        Nein(Status(p, "neu", null, "RO-1"));                                 // keine Kollision
        Nein(Status(p, "neu", null, "KOL-99"));
        Nein(p.Ausfuehren("kollision_status", new JsonObject { ["kollisionen"] = new JsonArray(), ["status"] = "neu" }));
        // erledigt, aber weiterhin gefunden: Status bleibt, Hinweis
        var l = Ok(Pruefen(p));
        Assert.Equal("erledigt", Hole(p, "KOL-1").Text("status"));
        Assert.Contains("KOL-1", Hinweis(l, "Als erledigt markiert"));
    }

    [Fact]
    public void Kollisionsliste_FormatFilterZusammenfassung_HandangelegteBleibenUnberuehrt()
    {
        var p = Wandprojekt();
        Ok(Rohr(p, "RO-1", "DN 20", [3000, -500, 1000], [3000, 500, 1000]));
        Ok(Rohr(p, "RO-2", "DN 20", [1000, 112, 1000], [2000, 112, 1000]));    // 0.5 mm an W-1: kontakt
        // von Hand angelegte Kollision ohne Kennung (wie im BCF-Test): der Prüflauf führt sie nicht nach
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Kollision", ["kennzeichen"] = "KOL-H1", ["eigenschaften"] = new JsonObject { ["art"] = "hart", ["beteiligte"] = Par("RO-1") },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([0, 0, 0]) },
        }));
        Ok(Pruefen(p));
        Ok(Pruefen(p));
        Assert.Null(Hole(p, "KOL-H1").Text("status"));
        var o = (JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject());
        Assert.Equal("Kollisionsliste", o["ableitung"]!.GetValue<string>());
        Assert.NotNull(o["modellStand"]);
        Assert.Equal(3, o["anzahl"]!.GetValue<int>());
        var z = o["zeilen"]!.AsArray().Single(x => x!["typ"]!.GetValue<string>() == "durchdringung")!;
        Assert.Equal("hart", z["art"]!.GetValue<string>());
        Assert.Equal("neu", z["status"]!.GetValue<string>());
        Assert.Equal(112.5, z["mass_mm"]!.GetValue<double>());
        Assert.Equal("EG", z["geschoss"]!.GetValue<string>());
        Assert.Equal("RO-1 (Rohr) ↔ W-1 (Wand)", string.Join(" ↔ ", z["beteiligte"]!.GetValue<string>().Split(" ↔ ").Order()));
        Assert.Equal("W-1 (Wand)", z["bauteil"]!.GetValue<string>());
        Assert.Equal("[3000,0,1000]", z["lage_mm"]!.ToJsonString());
        Assert.Equal(2, z["beteiligteListe"]!.AsArray().Count);
        Assert.Contains(" · Lauf 1 · ", z["pruefung"]!.GetValue<string>());     // zuerst gefunden in Lauf 1
        Assert.Contains(" · Lauf 2 · ", o["kollisionslauf"]!.GetValue<string>()); // letzter Lauf
        Assert.Equal(1, o["zusammenfassung"]!["jeTyp"]!["durchdringung"]!.GetValue<int>());
        Assert.Equal(2, o["zusammenfassung"]!["jeArtTyp"]!["hart kontakt"]!.GetValue<int>());   // RO-2 und KOL-H1 (ohne Typ = kontakt)
        // Filter
        Assert.Equal(1, ((JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["typ"] = "durchdringung" }))["anzahl"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["element"] = "RO-1" }))["anzahl"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["element"] = "W-1" }))["anzahl"]!.GetValue<int>());
        Assert.Equal(2, ((JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["status"] = "neu, bestaetigt", ["geschoss"] = "EG", ["art"] = "hart" }))["anzahl"]!.GetValue<int>());   // KOL-H1 ohne Geschoss
        Assert.Equal(0, ((JsonObject)Tuer.Abfrage(p, "kollisionen", new JsonObject { ["art"] = "weich" }))["anzahl"]!.GetValue<int>());
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "kollisionen", new JsonObject { ["status"] = "kaputt" }));
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "kollisionen", new JsonObject { ["geschoss"] = "W-1" }));
        // Listenansicht: der Katalog kennt liste = kollisionen
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-LI-KOL", ["eigenschaften"] = new JsonObject { ["art"] = "liste", ["liste"] = "kollisionen" } }));
    }
}
