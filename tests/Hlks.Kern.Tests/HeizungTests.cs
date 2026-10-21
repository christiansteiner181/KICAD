using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.HeizungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil A-D (entwurf/modul-5.md, Punkte 1–10): Medienpaar und Systempaar, neue Heizungsklassen, Heizgruppe (Ableitung von Gliedern,
/// Verbrauchern, Unterverteilern und Ästen), Leistungssummen «Σ Normleistung (Angabe), nicht umgerechnet», Befunde P33–P39, Befehle und Abfragen.
/// Alle Netze, Leistungen und Temperaturen FIKTIV (HeizungHilfe); keine Normwerte.</summary>
public class HeizungTests
{
    // ================================================================== Katalog

    [Theory]
    [InlineData("HZ-VL", "HZ-RL")]
    [InlineData("HZ-VL-Glyk", "HZ-RL-Glyk")]
    [InlineData("KAE-VL", "KAE-RL")]
    [InlineData("KAE-VL-Glyk", "KAE-RL-Glyk")]
    public void Medienpaar_GegenstueckBeidseitig_RolleNachVerbraucherrichtung(string vl, string rl)
    {
        Assert.Equal(rl, Kat.Medien[vl].Gegenstueck);
        Assert.Equal(vl, Kat.Medien[rl].Gegenstueck);
        Assert.Equal(rl, Kat.GegenstueckMedium(vl));
        Assert.Equal(vl, Kat.GegenstueckMedium(rl.ToLowerInvariant()));
        Assert.Equal("vorlauf", Kat.PaarRolle(vl));
        Assert.Equal("ruecklauf", Kat.PaarRolle(rl));
        Assert.True(Heizgruppen.IstHeizmedium(Kat, vl));
    }

    [Theory]
    [InlineData("KW")]
    [InlineData("SW")]
    [InlineData("ZUL")]
    [InlineData("*")]
    [InlineData(null)]
    public void Medienpaar_AndereMedien_OhnePaar(string? medium)
    {
        Assert.Null(Kat.GegenstueckMedium(medium));
        Assert.Null(Kat.PaarRolle(medium));
    }

    [Fact]
    public void Klassen_HeizungNeu_AnschluesseIfcUndBkp()
    {
        var hg = Kat.Klassen["Heizgruppe"];
        Assert.Equal(("system", "heizung", "IfcDistributionCircuit"), (hg.Schicht, hg.Gewerk, hg.Ifc));
        Assert.Equal(["ungemischt", "gemischt", "einspritzung", "drossel", "umlenk"], hg.Eigenschaften.Single(e => e.Name == "art").Auswahl!);
        Assert.Contains("kaelte", hg.Eigenschaften.Single(e => e.Name == "verbraucherart").Auswahl!);
        // Temperaturen: Eingaben ohne Vorgabewert
        Assert.All(new[] { "vlTemp", "rlTemp" }, n => Assert.Equal("°C", hg.Eigenschaften.Single(e => e.Name == n).Einheit));

        string Bild(string klasse) => string.Join(" ", Kat.Klassen[klasse].Anschluesse.Select(a => $"{a.Name}:{a.Medium}:{a.Fluss}"));
        Assert.True(Kat.Klassen["Mischventil"].IstArt("Armatur"));
        Assert.Equal("a:*:rein b:*:rein ab:*:raus", Bild("Mischventil"));
        Assert.True(Kat.Klassen["Fuehler"].IstArt("Armatur"));
        Assert.Equal(["temperatur", "druck", "thermometer", "manometer", "stroemung"], Kat.Klassen["Fuehler"].Eigenschaften.Single(e => e.Name == "art").Auswahl!);
        Assert.Equal("anschluss:*:beide", Bild("Ausdehnungsgefaess"));
        Assert.Equal("p_vl:*:rein p_rl:*:raus s_vl:*:raus s_rl:*:rein", Bild("Waermetauscher"));
        Assert.Equal("vl:KAE-VL:raus rl:KAE-RL:rein", Bild("Kaelteerzeuger"));
        foreach (var k in new[] { "Ausdehnungsgefaess", "Waermetauscher", "Kaelteerzeuger" })
            Assert.Equal(("zentrale", "heizung"), (Kat.Klassen[k].Rolle, Kat.Klassen[k].Gewerk));
        // BKP wie die bestehenden Heizungsklassen (242 Erzeugung/Speicher); Kälteerzeuger bewusst ohne Nummer (offene Frage an Christian)
        Assert.Equal("242", Kat.Klassen["Ausdehnungsgefaess"].Bkp);
        Assert.Equal("242", Kat.Klassen["Waermetauscher"].Bkp);
        Assert.Null(Kat.Klassen["Kaelteerzeuger"].Bkp);
        Assert.Contains("BKP offen", Kat.Klassen["Kaelteerzeuger"].Beschreibung);
        // Ventil: neue Heizungsarmaturen, «mischventil» bleibt lesbar
        var art = Kat.Klassen["Ventil"].Eigenschaften.Single(e => e.Name == "art").Auswahl!;
        Assert.Contains("mischventil", art);
        Assert.All(new[] { "strangregulierventil", "sicherheitsventil", "ueberstroemventil", "thermostatventil", "zonenventil", "entlueftung", "entleerung", "schmutzfaenger" },
            a => Assert.Contains(a, art));
    }

    [Theory]
    [InlineData("vl_2", "rl_2")]
    [InlineData("rl_2", "vl_2")]
    [InlineData("vl", "rl")]
    [InlineData("vl_aus", "rl_ein")]
    [InlineData("vl_ein", "rl_aus")]
    [InlineData("p_vl", "p_rl")]
    [InlineData("s_rl", "s_vl")]
    [InlineData("VL_1", "rl_1")]
    [InlineData("ein", null)]
    [InlineData("vlx", null)]
    public void PartnerAnschluss_NachDemNamen(string name, string? erwartet) => Assert.Equal(erwartet, Heizgruppen.PartnerAnschluss(name));

    [Theory]
    [InlineData("H VL NT", "H RL NT", true)]
    [InlineData("H-VL-NT", "h rl nt", true)]
    [InlineData("Heizung Vorlauf", "Heizung Rücklauf", true)]
    [InlineData("H VL NT", "H RL HT", false)]
    [InlineData("H VL VL", "H RL VL", false)]   // mehrdeutig
    [InlineData("Kaltwasser", "Warmwasser", false)]
    public void Systemnamen_VorlaufRuecklaufPassen(string vl, string rl, bool passt) => Assert.Equal(passt, Heizgruppen.NamenPassen(vl, rl));

    // ================================================================== Systempaar

    [Fact]
    public void SystemPaarAnlegen_LegtBeideSystemeAn_GegenstueckBeidseitig()
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Anlage","kennzeichen":"A-HZG"}""");
        var e = Gut(p, "system_paar_anlegen", """{"systemtyp":"Heizung Vorlauf","anlage":"A-HZG","name":"Heizung NT"}""");
        var vl = Hole(p, "HZ-VL"); var rl = Hole(p, "HZ-RL");
        Assert.Equal(("HZ-VL", "Heizung Vorlauf", "Heizung NT Vorlauf"), (vl.Text("medium"), vl.Text("systemtyp"), vl.Name));
        Assert.Equal(("HZ-RL", "Heizung Rücklauf", "Heizung NT Rücklauf"), (rl.Text("medium"), rl.Text("systemtyp"), rl.Name));
        Assert.Equal(Hole(p, "A-HZG").Id, vl.TeilVon);
        Assert.Equal(Hole(p, "A-HZG").Id, rl.TeilVon);
        Assert.Equal(rl.Id, vl.Bezuege["gegenstueck"]);
        Assert.Equal(vl.Id, rl.Bezuege["gegenstueck"]);
        Assert.True(Heizgruppen.IstPaar(p.Modell, vl, rl));
        Assert.Contains(e.Hinweise, h => h.Contains("Systempaar HZ-VL (HZ-VL) ↔ HZ-RL (HZ-RL) in A-HZG"));
        // zweites Mal: nichts zu tun
        var e2 = Gut(p, "system_paar_anlegen", """{"vorlauf":"HZ-VL","ruecklauf":"HZ-RL"}""");
        Assert.Contains(e2.Hinweise, h => h.Contains("schon ein Systempaar"));
        // ein weiteres Paar bekommt freie Kennzeichen (HZ-VL ist vergeben)
        Gut(p, "system_paar_anlegen", """{"systemtyp":"Heizung Vorlauf","anlage":"A-HZG"}""");
        Assert.Equal(4, p.Modell.DerKlasse("System").Count());
        Assert.All(p.Modell.DerKlasse("System"), s => Assert.True(Heizgruppen.Gegenstueck(p.Modell, s) is { } g && Heizgruppen.IstPaar(p.Modell, s, g) | Heizgruppen.IstPaar(p.Modell, g, s)));
        // system_lesen nennt das Gegenstück
        var text = (string)Tuer.Abfrage(p, "system_lesen", O("""{"system":"HZ-VL"}"""))!;
        Assert.Contains("Systempaar", text);
        Assert.Contains("HZ-RL", text);
    }

    [Fact]
    public void SystemPaarAnlegen_KoppeltBestehende_LehntFalscheMedienUndDoppeltesPaarAb()
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"Anlage","kennzeichen":"A-KAE"}""");
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"KAE-VL","teilVon":"A-KAE","eigenschaften":{"systemtyp":"Kälte Vorlauf"}}""");
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"KAE-RL","teilVon":"A-KAE","eigenschaften":{"systemtyp":"Kälte Rücklauf"}}""");
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"KW","eigenschaften":{"systemtyp":"Trinkwasser kalt"}}""");
        Assert.Null(Heizgruppen.Gegenstueck(p.Modell, Hole(p, "KAE-VL")));
        Abgelehnt(p, "system_paar_anlegen", "{}");                                                             // unklar, welches Paar
        Abgelehnt(p, "system_paar_anlegen", """{"systemtyp":"Heizung Rücklauf"}""");                             // kein Vorlauf
        Abgelehnt(p, "system_paar_anlegen", """{"systemtyp":"Trinkwasser kalt"}""");                             // kein Medienpaar
        Abgelehnt(p, "system_paar_anlegen", """{"vorlauf":"KAE-RL","ruecklauf":"KAE-VL"}""");                    // vertauscht
        Abgelehnt(p, "system_paar_anlegen", """{"vorlauf":"KAE-VL","ruecklauf":"KW"}""");                        // falsches Medium
        Abgelehnt(p, "system_paar_anlegen", """{"vorlauf":"A-KAE"}""");                                         // kein System
        Gut(p, "system_paar_anlegen", """{"vorlauf":"KAE-VL","ruecklauf":"KAE-RL"}""");
        Assert.True(Heizgruppen.IstPaar(p.Modell, Hole(p, "KAE-VL"), Hole(p, "KAE-RL")));
        // schon gepaart: nicht umhängen
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"KAE-RL2","teilVon":"A-KAE","eigenschaften":{"systemtyp":"Kälte Rücklauf"}}""");
        var e = Abgelehnt(p, "system_paar_anlegen", """{"vorlauf":"KAE-VL","ruecklauf":"KAE-RL2"}""");
        Assert.Contains("schon mit KAE-RL gepaart", e.Fehler);
        Assert.Contains("gegenstueck", e.Vorschlag);
        // nur der Rücklauf angegeben: das neue Vorlaufsystem bekommt das Gegenstück-Medium und die Anlage des Rücklaufs
        Gut(p, "system_paar_anlegen", """{"ruecklauf":"KAE-RL2","vorlauf":"KAE-VL2"}""");
        Assert.Equal(("KAE-VL", Hole(p, "A-KAE").Id), (Hole(p, "KAE-VL2").Text("medium"), Hole(p, "KAE-VL2").TeilVon));
        Assert.True(Heizgruppen.IstPaar(p.Modell, Hole(p, "KAE-VL2"), Hole(p, "KAE-RL2")));
    }

    [Fact]
    public void Systempaar_Nachfuehrung_GegenstueckSetzenUndLoesen_BeidseitigImSelbenPaket()
    {
        var p = Neu();
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"HZ-VL","eigenschaften":{"systemtyp":"Heizung Vorlauf"}}""");
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"HZ-RL","eigenschaften":{"systemtyp":"Heizung Rücklauf"}}""");
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"KW","eigenschaften":{"systemtyp":"Trinkwasser kalt"}}""");
        var e = Gut(p, "element_aendern", """{"element":"HZ-VL","bezuege":{"gegenstueck":"HZ-RL"}}""");
        Assert.Equal(Hole(p, "HZ-VL").Id, Hole(p, "HZ-RL").Bezuege["gegenstueck"]);
        Assert.Contains(e.Hinweise, h => h.Contains("Gegenstück nachgeführt"));
        Gut(p, "element_aendern", """{"element":"HZ-RL","bezuege":{"gegenstueck":null}}""");
        Assert.False(Hole(p, "HZ-VL").Bezuege.ContainsKey("gegenstueck"));
        Assert.False(Hole(p, "HZ-RL").Bezuege.ContainsKey("gegenstueck"));
        Assert.Contains("kein Medienpaar", Abgelehnt(p, "element_aendern", """{"element":"HZ-VL","bezuege":{"gegenstueck":"KW"}}""").Fehler);
        Assert.Contains("eigenes Gegenstück", Abgelehnt(p, "element_aendern", """{"element":"HZ-VL","bezuege":{"gegenstueck":"HZ-VL"}}""").Fehler);
    }

    // ================================================================== Ableitung der Heizgruppe

    [Fact]
    public void Heizgruppe_GliederVerbraucherUndArt_AusDemNetzAbgeleitet()
    {
        var p = Netz();
        var w1 = Weg(p, "HG-1");
        Assert.Equal("VT-1.vl_1/rl_1", w1.AbgangText);
        Assert.Equal("eigenschaft", w1.AbgangRlWoher);   // beim Anlegen gesetzt (Gegenstück von vl_1 nach dem Namen, Herkunft berechnung)
        Assert.Equal("berechnung", Hole(p, "HG-1").Eigenschaft("abgangRl")!.Herkunft.Art);
        Assert.Equal(("HZ-VL", "HZ-RL", true), (w1.Vorlauf?.Kennzeichen, w1.Ruecklauf?.Kennzeichen, w1.Systempaar));
        Assert.Equal(("HZ-VL", "HZ-RL"), (w1.MediumVl, w1.MediumRl));
        // Glieder in Fliessreihenfolge; das Mischventil liegt mit b auch im Rücklauf (Bypass), zählt aber einmal (Vorlauf, Eintritt a)
        Assert.Equal(["VE-1", "MV-1", "P-1"], w1.Glieder.Select(g => g.Element.Kennzeichen));
        Assert.All(w1.Glieder, g => Assert.Equal("vl", g.Weg));
        Assert.Equal([1, 2, 3], w1.Glieder.Select(g => g.Reihe));
        Assert.Equal("a", w1.Glieder.Single(g => g.Element.Kennzeichen == "MV-1").Eintritt);
        Assert.Equal(["HK-1"], w1.Verbraucher.Select(v => v.Kennzeichen));
        Assert.Equal(["MV-1"], w1.Mischer.Select(v => v.Kennzeichen));
        Assert.Equal(["P-1"], w1.Pumpen.Select(v => v.Kennzeichen));
        Assert.Empty(w1.Unterverteiler);
        Assert.Empty(w1.Aeste);
        Assert.Contains(Hole(p, "TS-1").Id, w1.Elemente);
        Assert.DoesNotContain(Hole(p, "VT-1").Id, w1.Elemente);   // der Abgang gehört nicht zur Gruppe
        var hg1 = Hole(p, "HG-1");
        Assert.Equal(("gemischt", "heizkoerper", "Heizkörper gemischt"), (hg1.Text("art"), hg1.Text("verbraucherart"), hg1.Name));
        Assert.Equal("berechnung", hg1.Eigenschaft("art")!.Herkunft.Art);
        Assert.Contains("Mischventil im Weg", hg1.Eigenschaft("art")!.Herkunft.Quelle);
        Assert.Equal("berechnung", hg1.Eigenschaft("verbraucherart")!.Herkunft.Art);
        Assert.Equal(Hole(p, "A-HZG").Id, hg1.TeilVon);   // Anlage aus dem Vorlaufsystem
        Assert.Equal((55.0, Testwert), (hg1.Zahl("vlTemp")!.Value, hg1.Eigenschaft("vlTemp")!.Herkunft.Quelle));

        var w2 = Weg(p, "HG-2");
        Assert.Equal(["P-2"], w2.Glieder.Select(g => g.Element.Kennzeichen));
        Assert.Equal(["HK-2"], w2.Verbraucher.Select(v => v.Kennzeichen));
        Assert.Equal(("ungemischt", "Heizkörper ungemischt"), (Hole(p, "HG-2").Text("art"), Hole(p, "HG-2").Name));
        // Zuordnung Element → Gruppe
        var stand = Heizgruppen.Ergebnis(p.Modell, p.Katalog);
        Assert.Equal(["HG-1"], stand.In(Hole(p, "HK-1").Id).Select(x => x.Gruppe.Kennzeichen));
        Assert.Equal(["HG-2"], stand.In(Hole(p, "R-R4").Id).Select(x => x.Gruppe.Kennzeichen));
        Assert.Empty(stand.In(Hole(p, "VT-1").Id));
        // keine Heizungsbefunde im sauberen Netz
        Assert.Empty(Pruefungen.Alle(p.Modell, p.Katalog).Where(b => HeizungPruefung.Codes.ContainsKey(b.Code)));
    }

    [Fact]
    public void Heizgruppe_Unterverteiler_UndAeste()
    {
        var p = NetzMitUnterverteilern();
        var e = Gut(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_1"}""");
        Assert.Contains(e.Hinweise, h => h.Contains("HG-1 «FBH ungemischt»") && h.Contains("2 Unterverteiler, 2 Verbraucher in 2 Ästen"));
        var w = Weg(p, "HG-1");
        Assert.Equal(["VT-2", "VT-3"], w.Unterverteiler.Select(u => u.Kennzeichen).Order());
        Assert.Equal(["FBH-1", "FBH-2"], w.Verbraucher.Select(u => u.Kennzeichen).Order());
        Assert.Equal("TS-V", w.Abzweig?.Kennzeichen);
        Assert.Equal(2, w.Aeste.Count);
        var ast2 = w.Aeste.Single(a => a.Unterverteiler.Any(u => u.Kennzeichen == "VT-2"));
        Assert.Equal(["FBH-1"], ast2.Verbraucher.Select(v => v.Kennzeichen));
        Assert.Equal(["VE-2"], ast2.Glieder.Select(v => v.Kennzeichen));
        Assert.Equal(["FBH-1"], w.KreiseVon(Hole(p, "VT-2")).Select(v => v.Kennzeichen));
        Assert.Equal(["P-1", "VE-1", "VE-2"], w.Glieder.Select(g => g.Element.Kennzeichen));
        Assert.Null(w.Glieder[0].Ast);
        Assert.Equal(ast2.Nummer, w.Glieder[2].Ast);
        Assert.Equal(("fbh", "ungemischt"), (Hole(p, "HG-1").Text("verbraucherart"), Hole(p, "HG-1").Text("art")));
        // element_lesen: Block «heizung» mit Ästen und Unterverteilern
        var o = (JsonObject)Tuer.Abfrage(p, "element_lesen", O("""{"element":"HG-1"}"""))!;
        Assert.Equal("TS-V", o["heizung"]!["abzweig"]!.GetValue<string>());
        Assert.Equal(2, o["heizung"]!["aeste"]!.AsArray().Count);
        Assert.Equal(1000.0, o["abgeleitet"]!["leistung_W"]!.GetValue<double>());
        // Leistung je Unterverteiler: VT-2 trägt FBH-1
        Assert.Equal(500.0, HeizungLeistung.Ergebnis(p.Modell, p.Katalog).Verteiler(Hole(p, "VT-2").Id)!.SummeW);
        Assert.Equal(1000.0, HeizungLeistung.Ergebnis(p.Modell, p.Katalog).Verteiler(Hole(p, "VT-1").Id)!.SummeW);
    }

    [Fact]
    public void Heizgruppe_GrenzeAnEinemVerteilerMitEigenerGruppe()
    {
        // Gruppe am Hauptverteiler, dahinter ein Verteiler, an dessen Abgang eine eigene Gruppe beginnt: der ist Grenze, nicht Unterverteiler
        var p = NetzMitUnterverteilern();
        Gut(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-2","verteiler":"VT-2","abgangVl":"vl_1"}""");
        Gut(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_1"}""");
        var w1 = Weg(p, "HG-1");
        Assert.Equal(["VT-2"], w1.Grenzen.Select(x => x.Kennzeichen));
        Assert.Equal(["VT-3"], w1.Unterverteiler.Select(x => x.Kennzeichen));
        Assert.Equal(["FBH-2"], w1.Verbraucher.Select(x => x.Kennzeichen));
        Assert.DoesNotContain(Hole(p, "VT-2").Id, w1.Elemente);
        Assert.Equal(["FBH-1"], Weg(p, "HG-2").Verbraucher.Select(x => x.Kennzeichen));
        var t = (string)Tuer.Abfrage(p, "heizung_lesen", O("{}"))!;
        Assert.Contains("Grenzen des Wegs: VT-2 Verteiler", t);
    }

    // ================================================================== Mischventil

    [Fact]
    public void Mischventil_AnschluesseFolgenDerArt_BypassImRuecklaufmedium()
    {
        var p = Netz(gruppen: false);
        string Bild(string kz) => string.Join(" ", Hole(p, kz).Anschluesse.Select(a => $"{a.Name}:{Netz.Medium(p.Modell, Hole(p, kz), a)}:{a.Fluss}"));
        // im Netz: a, ab im Vorlauf; b trägt das Gegenstück des Systemmediums (HZ-RL)
        Assert.Equal("a:HZ-VL:rein b:HZ-RL:rein ab:HZ-VL:raus", Bild("MV-1"));
        Punkt(p, "Mischventil", "MV-9", "HZ-V", 9000, 0, 500);   // ohne art: 3weg
        Assert.Equal("a:HZ-VL:rein b:HZ-RL:rein ab:HZ-VL:raus", Bild("MV-9"));
        Gut(p, "element_aendern", """{"element":"MV-9","eigenschaften":{"art":"4weg"}}""");
        Assert.Equal("a:HZ-VL:rein b:HZ-RL:rein ab:HZ-VL:raus b_aus:HZ-RL:raus", Bild("MV-9"));
        Gut(p, "element_aendern", """{"element":"MV-9","eigenschaften":{"art":"umschalt"}}""");
        Assert.Equal("a:HZ-VL:raus b:HZ-VL:raus ab:HZ-VL:rein", Bild("MV-9"));
        Gut(p, "element_aendern", """{"element":"MV-9","eigenschaften":{"art":"3weg"}}""");
        Assert.Equal("a:HZ-VL:rein b:HZ-RL:rein ab:HZ-VL:raus", Bild("MV-9"));
        Assert.Equal(HeizungNachfuehren.Anschlussbild("4weg").Select(x => x.Name), new[] { "a", "b", "ab", "b_aus" });
    }

    [Fact]
    public void Mischventil_4wegNach3weg_TrenntDieVerbindungAnBAus()
    {
        var p = Netz(gruppen: false);
        Punkt(p, "Mischventil", "MV-9", "HZ-V", 9000, 0, 500, O("""{"art":"4weg"}"""));
        Rohr(p, "R-9", "HZ-R", "MV-9.b_aus", null);
        Assert.NotNull(p.Modell.VerbindungAn(Hole(p, "MV-9").Id, "b_aus"));
        var e = Gut(p, "element_aendern", """{"element":"MV-9","eigenschaften":{"art":"3weg"}}""");
        Assert.Null(Hole(p, "MV-9").AnschlussNamens("b_aus"));
        Assert.Contains(e.Hinweise, h => h.Contains("MV-9.b_aus") && h.Contains("getrennt"));
    }

    // ================================================================== Leistungssummen

    [Fact]
    public void Leistung_SummenJeGruppeAnlageVerteilerLeitungUndRaum_Beschriftet()
    {
        var p = Netz();
        JsonObject L(string json) => (JsonObject)Tuer.Abfrage(p, "heizung_leistung", O(json))!;
        var g = L("""{"heizgruppe":"HG-1"}""");
        Assert.Equal((800.0, 1), (g["summeW"]!.GetValue<double>(), g["verbraucher"]!.GetValue<int>()));
        Assert.Equal("Σ Normleistung der Verbraucher (Angabe)", g["beschriftung"]!.GetValue<string>());
        Assert.Contains("nicht auf Auslegungstemperaturen umgerechnet", g["hinweis"]!.GetValue<string>());
        Assert.Contains("kein Volumenstrom, keine Stoffwerte", g["hinweis"]!.GetValue<string>());
        Assert.Equal(10.0, g["spreizungK"]!.GetValue<double>());
        Assert.Equal(Testwert, g["verbraucherListe"]![0]!["herkunft"]!.GetValue<string>().Split(", ", 2)[1]);

        var a = L("""{"anlage":"A-HZG"}""")["anlagen"]![0]!;
        Assert.Equal(1400.0, a["summeW"]!.GetValue<double>());
        Assert.Equal(["HG-1", "HG-2"], a["heizgruppen"]!.AsArray().Select(x => x!["heizgruppe"]!.GetValue<string>()));
        Assert.Equal(1400.0, a["verteiler"]![0]!["summeW"]!.GetValue<double>());

        var vl = L("""{"leitung":"R-V1"}""");
        Assert.Equal((800.0, "vl"), (vl["summeW"]!.GetValue<double>(), vl["weg"]!.GetValue<string>()));
        Assert.Contains("HK-1 800", vl["rechenweg"]!.GetValue<string>());
        Assert.Equal(["HG-1"], vl["heizgruppen"]!.AsArray().Select(x => x!.GetValue<string>()));
        var rl = L("""{"leitung":"R-R3"}""");
        Assert.Equal((800.0, "rl"), (rl["summeW"]!.GetValue<double>(), rl["weg"]!.GetValue<string>()));
        Assert.Equal(800.0, L("""{"leitung":"R-R2"}""")["summeW"]!.GetValue<double>());   // Bypass: Rücklauf von HK-1 stromab

        var r = L("""{"raum":"R-1"}""");
        Assert.Equal((1400.0, 2), (r["summeW"]!.GetValue<double>(), r["verbraucher"]!.GetValue<int>()));
        Assert.Null(r["heizleistungRaum"]);
        Assert.Contains("kein Befund", r["vergleich"]!.GetValue<string>());

        // warum: Rechenweg der Gruppensumme; element_lesen der Leitung
        var wy = (JsonObject)Tuer.Abfrage(p, "warum", O("""{"element":"HG-1","eigenschaft":"leistung"}"""))!;
        Assert.Contains("HK-1 800 = 800 W; nicht umgerechnet", wy["rechenweg"]!.GetValue<string>());
        var sp = (JsonObject)Tuer.Abfrage(p, "warum", O("""{"element":"HG-1","eigenschaft":"spreizung"}"""))!;
        Assert.Contains("|55 − 45| = 10 K", sp["rechenweg"]!.GetValue<string>());
        var el = (JsonObject)Tuer.Abfrage(p, "element_lesen", O("""{"element":"R-V1"}"""))!;
        Assert.Equal(800.0, el["abgeleitet"]!["summeLeistung_W"]!.GetValue<double>());
        Assert.Equal(["HG-1"], el["heizung"]!["heizgruppen"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public void Leistung_kWWirdInWUmgerechnet_OhneAngabeZaehltNicht()
    {
        var e = new Element { Id = "x", Kennzeichen = "LE-1", Klasse = "Heizkoerper" };
        Assert.Null(HeizungLeistung.LeistungW(Kat, e));
        e.Eigenschaften["leistung"] = new Wert { Inhalt = JsonValue.Create(1.5), Einheit = "kW", Herkunft = new Herkunft { Art = "mensch", Quelle = Testwert } };
        Assert.Equal(1500.0, HeizungLeistung.LeistungW(Kat, e));
        e.Eigenschaften["leistung"] = new Wert { Inhalt = JsonValue.Create(700.0), Herkunft = new Herkunft { Art = "mensch", Quelle = Testwert } };
        Assert.Equal(700.0, HeizungLeistung.LeistungW(Kat, e));   // Einheit der Klasse: W

        var p = Netz();
        Gut(p, "element_aendern", """{"element":"HK-2","eigenschaften":{"leistung":null}}""");
        var a = ((JsonObject)Tuer.Abfrage(p, "heizung_leistung", O("{}"))!)["anlagen"]![0]!;
        Assert.Equal((800.0, 2), (a["summeW"]!.GetValue<double>(), a["verbraucher"]!.GetValue<int>()));
        Assert.Equal(["HK-2"], a["ohneAngabe"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    // ================================================================== Befunde P33–P39

    [Fact]
    public void P33_VerbraucherNurEinseitig_NichtAngeschlossen_OderNichtImSystempaar()
    {
        var p = Netz();
        Assert.Empty(Befunde(p, "P33"));
        Punkt(p, "Heizkoerper", "HK-3", null, 6000, 0, 500, Leistung(300), "R-1");        // nichts angeschlossen
        Punkt(p, "Heizkoerper", "HK-4", null, 6000, 1000, 500, Leistung(300), "R-1");     // nur der Rücklauf
        Rohr(p, "R-HK4", "HZ-R", "HK-4.rl", null);
        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"HZ-VL2","teilVon":"A-HZG","eigenschaften":{"systemtyp":"Heizung Vorlauf"}}""");
        Strang(p, "HZ-V2", "HZ-VL2");
        Punkt(p, "Heizkoerper", "HK-5", null, 6000, 2000, 500, Leistung(300), "R-1");     // Vorlauf in HZ-VL2 (ohne Paar), Rücklauf in HZ-RL
        Rohr(p, "R-HK5-V", "HZ-V2", null, "HK-5.vl");
        Rohr(p, "R-HK5-R", "HZ-R", "HK-5.rl", null);
        var b = Befunde(p, "P33").ToDictionary(x => x.Element);
        Assert.Equal(["HK-3", "HK-4", "HK-5"], b.Keys.Order());
        Assert.Equal("hinweis", b["HK-3"].Schwere);
        Assert.Contains("noch nicht angeschlossen", b["HK-3"].Text);
        Assert.Equal("warnung", b["HK-4"].Schwere);
        Assert.Contains("nur ein Anschluss von vl/rl verbunden – vl (HZ-VL) ist offen", b["HK-4"].Text);
        Assert.Equal("warnung", b["HK-5"].Schwere);
        Assert.Contains("vl hängt am System HZ-VL2, rl am System HZ-RL – die beiden sind kein Systempaar (HZ-VL2 hat kein Gegenstück)", b["HK-5"].Text);
        Assert.Contains("system_paar_anlegen", b["HK-5"].Vorschlag);
    }

    [Fact]
    public void P34_HeizgruppeOhnePumpe_OhneAbgang_OhneVerbraucher_OhneSystempaar()
    {
        var p = Netz(pumpe2: false);
        var b = Befunde(p, "P34");
        Assert.Equal("HG-2", Assert.Single(b).Element);
        Assert.Contains("Heizgruppe ohne Pumpe (ab VT-1.vl_2/rl_2)", b[0].Text);

        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-2","verteiler":null}""");
        Assert.Null(Hole(p, "HG-2").Text("abgangVl"));
        Assert.Contains(Befunde(p, "P34"), x => x.Element == "HG-2" && x.Text.Contains("ohne Abgang"));

        Gut(p, "element_anlegen", """{"klasse":"Heizgruppe","kennzeichen":"HG-9","teilVon":"A-HZG","eigenschaften":{"abgangVl":"vl_ein"},"bezuege":{"verteiler":"VT-1","vorlauf":"HZ-VL","ruecklauf":"HZ-RL"}}""");
        Assert.Contains(Befunde(p, "P34"), x => x.Element == "HG-9" && x.Text.Contains("ohne Verbraucher"));

        Gut(p, "element_anlegen", """{"klasse":"System","kennzeichen":"HZ-VL2","teilVon":"A-HZG","eigenschaften":{"systemtyp":"Heizung Vorlauf"}}""");
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","vorlauf":"HZ-VL2"}""");
        Assert.Contains(Befunde(p, "P34"), x => x.Element == "HG-1" && x.Text.Contains("HZ-VL2 und HZ-RL sind nicht gegenseitig Gegenstück"));
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","vorlauf":null}""");
        Assert.Contains(Befunde(p, "P34"), x => x.Element == "HG-1" && x.Text.Contains("Vorlaufsystem fehlt"));
    }

    [Fact]
    public void P35_MischventilVertauscht_BypassOffen()
    {
        var p = Netz(vertauscht: true);
        var b = Befunde(p, "P35");
        Assert.Contains(b, x => x.Element == "MV-1" && x.Text.Contains("ab zeigt zum Abgang VT-1, nicht zu den Verbrauchern (a und ab vertauscht)"));
        Assert.Equal("ab", Weg(p, "HG-1").Glieder.Single(g => g.Element.Kennzeichen == "MV-1").Eintritt);
        Assert.Equal("gemischt", Hole(p, "HG-1").Text("art"));   // trotzdem eine gemischte Gruppe

        var q = Netz(bypass: false);
        var c = Befunde(q, "P35");
        Assert.Equal("MV-1", Assert.Single(c).Element);
        Assert.Contains("b (Bypass) ist offen", c[0].Text);
        Assert.Equal("warnung", c[0].Schwere);
        Assert.Empty(Befunde(Netz(), "P35"));
    }

    [Fact]
    public void P36_TemperaturenFehlen_OderWidersprechenSich_KaelteUmgekehrt()
    {
        var p = Netz();
        Assert.Empty(Befunde(p, "P36"));
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","rlTemp":null}""");
        var b = Assert.Single(Befunde(p, "P36"));
        Assert.Equal(("hinweis", "HG-1"), (b.Schwere, b.Element));
        Assert.Contains("Rücklauf fehlt (Eingabe des Planers, kein Vorgabewert)", b.Text);
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","vlTemp":40,"rlTemp":45,"quelle":"Testwert (fiktiv)"}""");
        b = Assert.Single(Befunde(p, "P36"));
        Assert.Equal("warnung", b.Schwere);
        Assert.Contains("Vorlauf 40 °C ist nicht höher als Rücklauf 45 °C", b.Text);
        // Kälte (verbraucherart kaelte bzw. Medium KAE): Vorlauf kälter als Rücklauf
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","verbraucherart":"kaelte","vlTemp":12,"rlTemp":6}""");
        Assert.Contains("Kälte: Vorlauf 12 °C ist nicht tiefer als Rücklauf 6 °C", Assert.Single(Befunde(p, "P36")).Text);
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","vlTemp":6,"rlTemp":12}""");
        Assert.Empty(Befunde(p, "P36"));
        Assert.Equal(6.0, Heizgruppen.Spreizung(Hole(p, "HG-1")));
        Assert.Equal("mensch", Hole(p, "HG-1").Eigenschaft("vlTemp")!.Herkunft.Art);
    }

    [Fact]
    public void P37_OhneHeizgruppe_JeAnlage_DannJeElement()
    {
        var p = Netz(gruppen: false);
        var b = Assert.Single(Befunde(p, "P37"));
        Assert.Equal(("hinweis", "A-HZG"), (b.Schwere, b.Element));
        Assert.Contains("Anlage ohne Heizgruppe: 2 Heizkoerper, 1 Mischventil, 2 Pumpe in keiner Heizgruppe", b.Text);
        Assert.Contains("heizgruppen_vorschlagen anlage A-HZG", b.Vorschlag);
        Gut(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_1"}""");
        Assert.Equal(["HK-2", "P-2"], Befunde(p, "P37").Select(x => x.Element).Order());
        Assert.All(Befunde(p, "P37"), x => Assert.Contains("liegt in keiner Heizgruppe", x.Text));
    }

    [Fact]
    public void P38_VentilArtMischventil_BleibtLesbar_HinweisAufUmstellung()
    {
        var p = Netz();
        Punkt(p, "Ventil", "VE-ALT", "HZ-V", 7000, 0, 500, O("""{"art":"mischventil","dn":25}"""));
        var b = Assert.Single(Befunde(p, "P38"));
        Assert.Equal(("hinweis", "VE-ALT"), (b.Schwere, b.Element));
        Assert.Contains("Mischventil anlegen", b.Vorschlag);
        // ein gespeicherter Stand mit diesem Ventil lädt ohne Umstellung
        var q = IfcHilfe.Leer();
        q.Modell.Laden(p.Modell.Abbild([]));
        Assert.Equal("mischventil", Hole(q, "VE-ALT").Text("art"));
        Assert.Equal("Ventil", Hole(q, "VE-ALT").Klasse);
        Assert.Single(Befunde(q, "P38"));
    }

    [Fact]
    public void P39_MascheImHeiznetz_JederVerbraucherZaehltEinmal()
    {
        var p = NetzMitMasche();
        var b = Assert.Single(Befunde(p, "P39"));
        Assert.Equal(("warnung", "TS-A"), (b.Schwere, b.Element));
        Assert.Contains("1 Verbraucher erreichen TS-A über mehrere Wege (HK-1)", b.Text);
        var netz = HeizungLeistung.Ergebnis(p.Modell, p.Katalog);
        Assert.Equal(700.0, netz.Leitung(Hole(p, "R-1").Id)!.SummeW);       // einmal, nicht doppelt
        Assert.Equal(700.0, netz.Leitung(Hole(p, "R-2").Id)!.SummeW);
        Assert.Equal(700.0, netz.Leitung(Hole(p, "R-3").Id)!.SummeW);
        Assert.True(netz.MaschenLeitungen.SetEquals(new[] { Hole(p, "R-2").Id, Hole(p, "R-3").Id }));
        var l = (JsonObject)Tuer.Abfrage(p, "heizung_leistung", O("{}"))!;
        Assert.Equal(["TS-A"], l["maschen"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    // ================================================================== Befehle

    [Fact]
    public void HeizgruppeAnlegen_AbgangAusDerPumpe_HerkunftBerechnung()
    {
        var p = Netz(gruppen: false);
        var e = Gut(p, "heizgruppe_anlegen", """{"pumpe":"P-1","vlTemp":35,"rlTemp":28}""");
        var hg = p.Modell.DerKlasse("Heizgruppe").Single();
        Assert.Equal("HG-1", hg.Kennzeichen);
        Assert.Equal(Hole(p, "VT-1").Id, hg.Bezuege["verteiler"]);
        Assert.Equal("vl_1", hg.Text("abgangVl"));
        Assert.Equal("berechnung", hg.Eigenschaft("abgangVl")!.Herkunft.Art);
        Assert.Contains("Gruppenpumpe P-1", hg.Eigenschaft("abgangVl")!.Herkunft.Quelle);
        Assert.Equal("rl_1", hg.Text("abgangRl"));
        Assert.Contains("Gegenstück von vl_1", hg.Eigenschaft("abgangRl")!.Herkunft.Quelle);
        Assert.Equal((35.0, "mensch"), (hg.Zahl("vlTemp")!.Value, hg.Eigenschaft("vlTemp")!.Herkunft.Art));
        Assert.Contains(e.Hinweise, h => h.StartsWith("HG-1 «Heizkörper gemischt»: ab VT-1.vl_1/rl_1, 3 Glieder (Mischventil MV-1, Pumpe P-1)"));
        // der einzige freie Abgang ist danach vl_2
        Gut(p, "heizgruppe_anlegen", """{"verteiler":"VT-1"}""");
        Assert.Equal("vl_2", Hole(p, "HG-2").Text("abgangVl"));
    }

    [Fact]
    public void HeizgruppeAnlegen_Fehlerfaelle()
    {
        var p = Netz(gruppen: false);
        Assert.Contains("Abgang fehlt", Abgelehnt(p, "heizgruppe_anlegen", "{}").Fehler);
        Assert.Contains("2 freie Vorlauf-Abgänge (vl_1, vl_2)", Abgelehnt(p, "heizgruppe_anlegen", """{"verteiler":"VT-1"}""").Fehler);
        Assert.Contains("keinen Anschluss «vl_9»", Abgelehnt(p, "heizgruppe_anlegen", """{"verteiler":"VT-1","abgangVl":"vl_9"}""").Fehler);
        Assert.Contains("keine Pumpe", Abgelehnt(p, "heizgruppe_anlegen", """{"pumpe":"HK-1"}""").Fehler);
        Assert.Contains("ist kein System", Abgelehnt(p, "heizgruppe_anlegen", """{"verteiler":"VT-1","abgangVl":"vl_1","vorlauf":"VT-1"}""").Fehler);
        Assert.Contains("kein Vorlauf", Abgelehnt(p, "heizgruppe_anlegen", """{"verteiler":"VT-1","abgangVl":"vl_1","vorlauf":"HZ-RL"}""").Fehler);
        Gut(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_1"}""");
        Assert.Contains("schon vergeben", Abgelehnt(p, "heizgruppe_anlegen", """{"kennzeichen":"HG-1","verteiler":"VT-1","abgangVl":"vl_2"}""").Fehler);
        Assert.Contains("keine Heizgruppe", Abgelehnt(p, "heizgruppe_aendern", """{"heizgruppe":"VT-1","name":"x"}""").Fehler);
        Assert.Contains("ist ein", Abgelehnt(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","anlage":"HZ-VL"}""").Fehler);
    }

    [Fact]
    public void HeizgruppeAendern_WerteSetzenUndEntfernen_AbgangWechseln()
    {
        var p = Netz();
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","name":"Heizkörper Süd","vlTemp":50,"quelle":"Auslegung Test (fiktiv)","beschreibung":"Testgruppe"}""");
        var hg = Hole(p, "HG-1");
        Assert.Equal(("Heizkörper Süd", 50.0, "Auslegung Test (fiktiv)", "Testgruppe"), (hg.Name, hg.Zahl("vlTemp")!.Value, hg.Eigenschaft("vlTemp")!.Herkunft.Quelle, hg.Text("beschreibung")));
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","vlTemp":null,"beschreibung":""}""");
        Assert.Null(Hole(p, "HG-1").Eigenschaft("vlTemp"));
        Assert.Null(Hole(p, "HG-1").Text("beschreibung"));
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","art":"einspritzung"}""");
        Assert.Equal(("einspritzung", "mensch"), (Hole(p, "HG-1").Text("art"), Hole(p, "HG-1").Eigenschaft("art")!.Herkunft.Art));
        // Abgang wechseln: HG-2 löschen, HG-1 an vl_2
        Gut(p, "element_loeschen", """{"element":"HG-2"}""");
        Gut(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","verteiler":"VT-1","abgangVl":"vl_2","abgangRl":"rl_2"}""");
        Assert.Equal(["HK-2"], Weg(p, "HG-1").Verbraucher.Select(v => v.Kennzeichen));
        Assert.Contains("keinen Anschluss «rl_7»", Abgelehnt(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","abgangRl":"rl_7"}""").Fehler);
        Assert.Contains("Rücklauf", Abgelehnt(p, "heizgruppe_aendern", """{"heizgruppe":"HG-1","ruecklauf":"HZ-VL"}""").Fehler);
    }

    [Fact]
    public void HeizgruppenVorschlagen_TrockenlaufZeigt_DannAnlegen_SystempaarWennFrei()
    {
        var p = Netz(gruppen: false, paar: false);
        var t = Gut(p, "heizgruppen_vorschlagen", "{}", trocken: true);
        Assert.Contains(t.Hinweise, h => h.Contains("«Heizkörper gemischt»: ab VT-1.vl_1/rl_1"));
        Assert.Contains(t.Hinweise, h => h.Contains("«Heizkörper ungemischt»: ab VT-1.vl_2/rl_2"));
        Assert.Empty(p.Modell.DerKlasse("Heizgruppe"));
        Assert.Null(Heizgruppen.Gegenstueck(p.Modell, Hole(p, "HZ-VL")));

        var e = Gut(p, "heizgruppen_vorschlagen", """{"anlage":"A-HZG"}""");
        Assert.Contains(e.Hinweise, h => h.Contains("Systempaar HZ-VL ↔ HZ-RL gesetzt"));
        Assert.True(Heizgruppen.IstPaar(p.Modell, Hole(p, "HZ-VL"), Hole(p, "HZ-RL")));
        var hg = p.Modell.DerKlasse("Heizgruppe").OrderBy(x => x.Kennzeichen).ToList();
        Assert.Equal(["Heizkörper gemischt", "Heizkörper ungemischt"], hg.Select(x => x.Name));
        Assert.All(hg, x => Assert.Equal(Hole(p, "A-HZG").Id, x.TeilVon));
        Assert.Contains("Gruppenpumpe P-1", hg[0].Eigenschaft("abgangVl")!.Herkunft.Quelle);
        Assert.Empty(Befunde(p, "P37"));
        Assert.Empty(Befunde(p, "P34"));
        // nochmals: nichts Neues, Bestehendes bleibt
        var n = Gut(p, "heizgruppen_vorschlagen", "{}");
        Assert.Contains(n.Hinweise, h => h.StartsWith("Keine neue Heizgruppe gefunden (bestehende Gruppen bleiben)"));
        Assert.Equal(2, p.Modell.DerKlasse("Heizgruppe").Count());
    }

    [Fact]
    public void HeizgruppenVorschlagen_PumpeOhneVerbraucherseite_Hinweis()
    {
        var p = Netz(gruppen: false);
        Punkt(p, "Pumpe", "P-9", "HZ-V", 8000, 0, 500);
        Rohr(p, "R-P9", "HZ-V", "P-9.druck", null);
        var e = Gut(p, "heizgruppen_vorschlagen", "{}", trocken: true);
        Assert.Contains(e.Hinweise, h => h.Contains("Pumpe P-9: nicht beidseitig") && h.Contains("kein Vorschlag"));
    }

    // ================================================================== Abfragen

    [Fact]
    public void HeizungLesen_TextansichtMitGruppenGliedernUndSumme()
    {
        var p = Netz();
        var t = (string)Tuer.Abfrage(p, "heizung_lesen", O("{}"))!;
        Assert.Contains("Systempaare: HZ-VL (HZ-VL) ↔ HZ-RL (HZ-RL)", t);
        Assert.Contains("Heizgruppen (2):", t);
        Assert.Contains("HG-1 «Heizkörper gemischt»", t);
        Assert.Contains("Vorlauf: VE-1 Ventil absperrventil → MV-1 Mischventil 3weg → P-1 Pumpe", t);
        Assert.Contains("Σ Normleistung der Verbraucher (Angabe): 800 W, nicht umgerechnet", t);
        Assert.Contains("Temperaturen (Eingabe): Vorlauf 55 °C", t);
        Assert.Contains("Spreizung 10 K", t);
        // element_lesen System: Gegenstück und Gruppen
        var s = (JsonObject)Tuer.Abfrage(p, "element_lesen", O("""{"element":"HZ-VL"}"""))!;
        Assert.Equal(("vorlauf", "HZ-RL", true), (s["heizung"]!["rolle"]!.GetValue<string>(), s["heizung"]!["gegenstueck"]!.GetValue<string>(), s["heizung"]!["systempaar"]!.GetValue<bool>()));
        Assert.Equal(["HG-1", "HG-2"], s["heizung"]!["heizgruppen"]!.AsArray().Select(x => x!.GetValue<string>()).Order());
        // element_lesen Heizgruppe: Glieder mit Reihe, Weg und Eintritt
        var g = (JsonObject)Tuer.Abfrage(p, "element_lesen", O("""{"element":"HG-1"}"""))!;
        var glieder = g["heizung"]!["glieder"]!.AsArray();
        Assert.Equal(["VE-1", "MV-1", "P-1"], glieder.Select(x => x!["element"]!.GetValue<string>()));
        Assert.Equal("a", glieder[1]!["eintritt"]!.GetValue<string>());
        Assert.Equal(10.0, g["abgeleitet"]!["spreizung_K"]!.GetValue<double>());
        Assert.Equal(800.0, g["heizung"]!["summeLeistungW"]!.GetValue<double>());
        // Abfragen stehen in der Liste der Tür
        var liste = Abfragen.Liste.Select(a => a.Name).ToList();
        Assert.Contains("heizung_lesen", liste);
        Assert.Contains("heizung_leistung", liste);
    }

    [Fact]
    public void HeizungLesen_AnlageOhneHeizgruppe_NenntDieOffenenElemente()
    {
        var p = Netz(gruppen: false);
        var t = (string)Tuer.Abfrage(p, "heizung_lesen", O("""{"anlage":"A-HZG"}"""))!;
        Assert.Contains("Heizgruppen (0):", t);
        Assert.Contains("Ohne Heizgruppe", t);
        Assert.Contains("P-1", t);
    }

    [Fact]
    public void Trockenlauf_UndZuruecknehmen_HeizgruppeVerschwindetWiederVollstaendig()
    {
        var p = Netz(gruppen: false);
        var vorher = p.Modell.Elemente.Count;
        Gut(p, "heizgruppe_anlegen", """{"verteiler":"VT-1","abgangVl":"vl_1"}""", trocken: true);
        Assert.Equal(vorher, p.Modell.Elemente.Count);
        Gut(p, "heizgruppe_anlegen", """{"verteiler":"VT-1","abgangVl":"vl_1"}""");
        Assert.Equal(vorher + 1, p.Modell.Elemente.Count);
    }
}
