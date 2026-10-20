using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.SanitaerHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 4 Teil A-K, Punkt 3 und 5: Belastungssummen im Netz (Trinkwasser stromab, Schmutzwasser stromauf, je Leitung, Strang, System,
/// Anlage), grösster Einzel-LU, Masche (Apparat einmal + P24), Zirkulation und Regenwasser ausgenommen, fehlende Art, Anschluss/Art-Widerspruch,
/// Apparat in keiner Anlage. Kleine Testnetze, Werte aus dem Katalog: WC wc_75 (LU KW 1, DU 2), Waschtisch (1/1/0.5), Badewanne (3/3/0.8).</summary>
public class SanitaerNetzTests
{
    /// <summary>KW: HA-1 → R1 → T1; T1.b → R2 → WT-1.kw; T1.c → R3 → T2.a; T2.b → R4 → WC-1.kw; T2.c → R5 → BW-1.kw.
    /// SW: WT-1.sw → S1 → T3.c; WC-1.sw → S2 → T3.a; T3.b → S3 → T4.a; BW-1.sw → S4 → T4.c; T4.b → S5 → KS-1.</summary>
    static Projekt Verzweigung()
    {
        var p = Netzprojekt(0.5, "KW", "SW");
        Apparat(p, "Waschtisch", "WT-1"); Apparat(p, "WC", "WC-1"); Apparat(p, "Badewanne", "BW-1", geschoss: "OG");
        Hausanschluss(p); Kontrollschacht(p);
        foreach (var t in new[] { "T1", "T2" }) Formteil(p, t, "KW-1");
        foreach (var t in new[] { "T3", "T4" }) Formteil(p, t, "SW-1");
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T1.b", "WT-1.kw");
        Rohr(p, "R3", "KW-1", "T1.c", "T2.a");
        Rohr(p, "R4", "KW-1", "T2.b", "WC-1.kw");
        Rohr(p, "R5", "KW-1", "T2.c", "BW-1.kw");
        Rohr(p, "S1", "SW-1", "WT-1.sw", "T3.c");
        Rohr(p, "S2", "SW-1", "WC-1.sw", "T3.a");
        Rohr(p, "S3", "SW-1", "T3.b", "T4.a");
        Rohr(p, "S4", "SW-1", "BW-1.sw", "T4.c");
        Rohr(p, "S5", "SW-1", "T4.b", "KS-1.ein");
        return p;
    }

    static void Tw(Projekt p, string leitung, double kw, double ww, double max, int apparate)
    {
        var b = Belastung(p, leitung);
        Assert.Equal("trinkwasser", b["netz"]!.GetValue<string>());
        Etwa(kw, Zahl(b["summeLuKw"]), $"{leitung} ΣLU KW");
        Etwa(ww, Zahl(b["summeLuWw"]), $"{leitung} ΣLU WW");
        Etwa(max, Zahl(b["groessterLu"]), $"{leitung} grösster LU");
        Assert.Equal(apparate, b["apparate"]!.GetValue<int>());
        Assert.Null(b["summeDu"]);
    }

    static void Aw(Projekt p, string leitung, double du, int apparate)
    {
        var b = Belastung(p, leitung);
        Assert.Equal("abwasser", b["netz"]!.GetValue<string>());
        Etwa(du, Zahl(b["summeDu"]), $"{leitung} ΣDU");
        Assert.Equal(apparate, b["apparate"]!.GetValue<int>());
        Assert.Null(b["summeLuKw"]);
    }

    [Fact]
    public void Verzweigung_Trinkwasser_SummenStromab_GroessterEinzelLu()
    {
        var p = Verzweigung();
        Tw(p, "R1", 1 + 1 + 3, 0, 3, 3);   // alles stromab des Hausanschlusses (nur KW angeschlossen)
        Tw(p, "R2", 1, 0, 1, 1);
        Tw(p, "R3", 1 + 3, 0, 3, 2);
        Tw(p, "R4", 1, 0, 1, 1);
        Tw(p, "R5", 3, 0, 3, 1);
        var r1 = Belastung(p, "R1");
        Assert.Equal(new[] { "BW-1", "WC-1", "WT-1" }, r1["beitraege"]!.AsArray().Select(b => b!["apparat"]!.GetValue<string>()));
        Assert.All(r1["beitraege"]!.AsArray(), b => Assert.Contains("Zeile", b!["quelle"]!.GetValue<string>()));
        Assert.Equal("vorgabe", r1["beitraege"]![0]!["artWoher"]!.GetValue<string>());
        Assert.Contains("BW-1 3", r1["rechenweg"]!.GetValue<string>());
        Assert.Equal("A-1", r1["anlage"]!.GetValue<string>());
        Assert.Equal("KW-1", r1["strang"]!.GetValue<string>());
    }

    [Fact]
    public void Verzweigung_Schmutzwasser_SummenStromauf()
    {
        var p = Verzweigung();
        Aw(p, "S1", 0.5, 1);
        Aw(p, "S2", 2, 1);
        Aw(p, "S3", 2.5, 2);
        Aw(p, "S4", 0.8, 1);
        Aw(p, "S5", 3.3, 3);
        Assert.Contains("ΣDU", Belastung(p, "S5")["rechenweg"]!.GetValue<string>());
    }

    [Fact]
    public void Summen_JeStrangSystemAnlage_VereinigungDerApparate()
    {
        var p = Verzweigung();
        // zweiter KW-Strang: nur R3–R5 (Abzweig), damit Strang und System verschieden sind
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Strang", ["kennzeichen"] = "KW-2", ["teilVon"] = "KW" }));
        foreach (var r in new[] { "R3", "R4", "R5" }) Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = r, ["teilVon"] = "KW-2" }));
        var st = Abfrage(p, "sanitaer_belastung", new JsonObject { ["strang"] = "KW-2" });
        Etwa(4, Zahl(st["summeLuKw"]), "Strang KW-2 ΣLU KW");
        Etwa(3, Zahl(st["groessterLu"]), "Strang KW-2 grösster LU");
        Assert.Equal(2, st["apparate"]!.GetValue<int>());
        Assert.Equal(3, st["leitungen"]!.AsArray().Count);
        var st1 = Abfrage(p, "sanitaer_belastung", new JsonObject { ["strang"] = "KW-1" });
        Etwa(5, Zahl(st1["summeLuKw"]), "Strang KW-1 (enthält R1: alles stromab)");
        var a = Abfrage(p, "sanitaer_belastung", new JsonObject { ["anlage"] = "A-1" })["anlagen"]![0]!;
        Etwa(5, Zahl(a["trinkwasser"]!["summeLuKw"]), "Anlage ΣLU KW");
        Etwa(3.3, Zahl(a["abwasser"]!["summeDu"]), "Anlage ΣDU");
        var kw = a["systeme"]!.AsArray().Single(s => s!["system"]!.GetValue<string>() == "KW")!;
        Etwa(5, Zahl(kw["summeLuKw"]), "System KW");
        Assert.Equal(2, kw["straenge"]!.AsArray().Count);
        Assert.Equal(5, kw["leitungen"]!.AsArray().Count);
        var sw = a["systeme"]!.AsArray().Single(s => s!["system"]!.GetValue<string>() == "SW")!;
        Assert.Equal("Apparate stromauf", sw["richtung"]!.GetValue<string>());
        // ohne Parameter: alle Anlagen
        var alle = Abfrage(p, "sanitaer_belastung");
        Assert.Single(alle["anlagen"]!.AsArray());
        Assert.Null(alle["maschen"]);
        Assert.Contains("keine Dimensionierung", alle["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public void Masche_ApparatZaehltEinmal_BefundP24()
    {
        // Ring: HA-1 → R1 → T1; T1.b → R2 → T2.a; T1.c → R3 → T2.c; T2.b → R4 → WC-1.kw  (zwei Wege von T1 nach T2)
        var p = Netzprojekt(0.5, "KW");
        Apparat(p, "WC", "WC-1"); Hausanschluss(p);
        Formteil(p, "T1", "KW-1"); Formteil(p, "T2", "KW-1");
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T1.b", "T2.a");
        Rohr(p, "R3", "KW-1", "T1.c", "T2.c");
        Rohr(p, "R4", "KW-1", "T2.b", "WC-1.kw");
        Tw(p, "R1", 1, 0, 1, 1);   // nicht 2: der Apparat zählt je Leitung einmal
        Tw(p, "R2", 1, 0, 1, 1);
        Tw(p, "R3", 1, 0, 1, 1);
        // «masche» nur an den Leitungen IN der Masche (zwischen Verzweigung T2 und Treffpunkt T1, Prüfbericht 4A B6); R1 und R4 tragen den Apparat eindeutig
        Assert.True(Belastung(p, "R2")["masche"]!.GetValue<bool>());
        Assert.True(Belastung(p, "R3")["masche"]!.GetValue<bool>());
        Assert.False(Belastung(p, "R1")["masche"]!.GetValue<bool>());
        Assert.False(Belastung(p, "R4")["masche"]!.GetValue<bool>());
        var p24 = Assert.Single(Sanitaerbefunde(p), b => b.Code == "P24");
        Assert.Equal(("warnung", "T1"), (p24.Schwere, p24.Element));   // am Treffpunkt
        Assert.Contains("Netz mit Masche – Summe nicht eindeutig", p24.Text);
        Assert.Contains("über mehrere Wege ab T2", p24.Text);             // der Text nennt die Verzweigung
        Assert.Contains("WC-1", p24.Text);
        var st = Abfrage(p, "sanitaer_belastung", new JsonObject { ["strang"] = "KW-1" });
        Etwa(1, Zahl(st["summeLuKw"]), "Strang: Apparat einmal");
        Assert.True(st["masche"]!.GetValue<bool>());
        Assert.Equal(new[] { "T1" }, Abfrage(p, "sanitaer_belastung")["maschen"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.DoesNotContain(Sanitaerbefunde(p), b => b.Code == "P26");
        // Masche auflösen (R3 löschen) → P24 behoben
        var e = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "R3" }));
        Assert.Contains(e.BehobeneBefunde, b => b.Code == "P24");
        Assert.False(Belastung(p, "R2")["masche"]!.GetValue<bool>());
    }

    [Fact]
    public void Masche_MitAbzweigInDerMasche_NurDieMaschenleitungenGekennzeichnet()
    {
        // Ring T1 → (R2 | R3) → T2 → R4 → T3; an T3 zwei Apparate (WC-1 über R5, WT-1 über R6); WT-2 direkt an T4 auf dem Weg R3 (R3a, R3b)
        var p = Netzprojekt(0.5, "KW");
        Apparat(p, "WC", "WC-1"); Apparat(p, "Waschtisch", "WT-1"); Apparat(p, "Waschtisch", "WT-2"); Hausanschluss(p);
        foreach (var t in new[] { "T1", "T2", "T3", "T4" }) Formteil(p, t, "KW-1");
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T1.b", "T2.a");
        Rohr(p, "R3a", "KW-1", "T1.c", "T4.a");
        Rohr(p, "R3b", "KW-1", "T4.b", "T2.c");
        Rohr(p, "R7", "KW-1", "T4.c", "WT-2.kw");
        Rohr(p, "R4", "KW-1", "T2.b", "T3.a");
        Rohr(p, "R5", "KW-1", "T3.b", "WC-1.kw");
        Rohr(p, "R6", "KW-1", "T3.c", "WT-1.kw");
        foreach (var r in new[] { "R2", "R3a", "R3b" }) Assert.True(Belastung(p, r)["masche"]!.GetValue<bool>(), r);
        foreach (var r in new[] { "R1", "R4", "R5", "R6", "R7" }) Assert.False(Belastung(p, r)["masche"]!.GetValue<bool>(), r);
        Tw(p, "R1", 3, 0, 1, 3);
        Tw(p, "R4", 2, 0, 1, 2);
        var p24 = Assert.Single(Sanitaerbefunde(p), b => b.Code == "P24");
        Assert.Equal("T1", p24.Element);
        Assert.Contains("ab T2", p24.Text);
        Assert.DoesNotContain(Sanitaerbefunde(p), b => b.Code == "P26");
    }

    /// <summary>Prüfbericht 4A B2, Gedankenbeispiel: HA-1.aus → R1 → T1.a; T1.b ↔ R2.ende (R2 verkehrt gezogen, von T2.a nach T1.b);
    /// R2.anfang ↔ T2.a; T2.b → R3 → WC-1.kw. Vom WC stromauf: R3, T2 – dann R2.anfang «rein»: der Weg endet. Kein P07 (beide ↔ rein/raus).</summary>
    static Projekt VerkehrteLeitung()
    {
        var p = Netzprojekt(0.5, "KW");
        Apparat(p, "WC", "WC-1"); Hausanschluss(p);
        Formteil(p, "T1", "KW-1"); Formteil(p, "T2", "KW-1");
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T2.a", "T1.b");   // verkehrt
        Rohr(p, "R3", "KW-1", "T2.b", "WC-1.kw");
        return p;
    }

    [Fact]
    public void OhneWeg_VerkehrteLeitungZwischenFormteilen_P26_NichtStill()
    {
        var p = VerkehrteLeitung();
        Tw(p, "R3", 1, 0, 1, 1);    // bis zur verkehrten Leitung zählt der Apparat
        Tw(p, "R2", 0, 0, 0, 0);    // dahinter fehlt er – deshalb der Befund
        Tw(p, "R1", 0, 0, 0, 0);
        var b = Sanitaerbefunde(p);
        Assert.DoesNotContain(Pruefungen.Alle(p.Modell, p.Katalog), x => x.Code == "P07");   // vorher gab es keinen Befund (Prüfbericht B2)
        var p26 = Assert.Single(b, x => x.Code == "P26");
        Assert.Equal(("warnung", "WC-1"), (p26.Schwere, p26.Element));
        Assert.StartsWith("Apparat ohne Weg im Netz – Belastung fehlt stromauf: kw (KW) ist verbunden, der Weg endet bei T2", p26.Text);
        Assert.Contains("T2.a ist mit R2.anfang «rein» verbunden – Fliessrichtung verkehrt", p26.Text);
        Assert.Contains("ohneWeg", p26.Vorschlag);
        // sanitaer_belastung: Liste ohneWeg je Anlage und gesamt
        var alle = Abfrage(p, "sanitaer_belastung");
        var ow = Assert.Single(alle["anlagen"]![0]!["ohneWeg"]!.AsArray())!;
        Assert.Equal(("WC-1", "kw", "KW", "stromauf", "T2"), (ow["apparat"]!.GetValue<string>(), ow["anschluss"]!.GetValue<string>(), ow["medium"]!.GetValue<string>(),
            ow["fehlt"]!.GetValue<string>(), ow["endetBei"]!.GetValue<string>()));
        Assert.Contains("R2.anfang", ow["grund"]!.GetValue<string>());
        Assert.Single(alle["ohneWeg"]!.AsArray());
        Assert.Single(Abfrage(p, "sanitaer_belastung", new JsonObject { ["anlage"] = "A-1" })["anlagen"]![0]!["ohneWeg"]!.AsArray());
        // Zusammenstellung: ΣLU KW enthält den Apparat, das Netz nicht – der Hinweis sagt es
        var z = Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-1" });
        Etwa(1, Zahl(z["summeLuKw"]), "ΣLU KW (Stückzahl)");
        Assert.Contains(z["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("ΣLU KW enthält 1 Apparat ohne Weg im KW-Netz: WC-1 – in den Netzsummen fehlen sie stromauf (Befund P26)"));
        // Leitung richtig herum neu ziehen → P26 behoben, R1 trägt den Apparat
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "R2" }));
        var e = Ok(p.Ausfuehren("leitung_ziehen", new JsonObject
        {
            ["kennzeichen"] = "R2", ["strang"] = "KW-1", ["dn"] = 20, ["von"] = "T1.b", ["nach"] = "T2.a", ["punkte"] = Pts([5000, 0, 100], [5000, 500, 100]),
        }));
        Assert.DoesNotContain(Sanitaerbefunde(p), x => x.Code == "P26");
        Tw(p, "R1", 1, 0, 1, 1);
        Assert.Null(Abfrage(p, "sanitaer_belastung")["ohneWeg"]);
    }

    static JsonArray ZweiAnschluesse() => new(new[] { "a", "b" }.Select(x => (JsonNode)new JsonObject { ["name"] = x, ["medium"] = "*", ["fluss"] = "beide" }).ToArray());

    [Fact]
    public void OhneWeg_UngerichteteFormteile_SeitenastMitReduktionKeinFehlalarm_VerkehrtHinterReduktionErkannt()
    {
        // Seitenast, der mit einer Reduktion am T-Stück beginnt (wie im Abnahmeszenario 3): HA → R1 → T1.a; T1.b → R2 → WC-1.kw;
        // T1.c ↔ RD.a (ungerichtet); RD.b → R3 → WT-1.kw. Vom WC her ist RD ein Seitenast (RD.b fliesst weg) – kein P26.
        var p = Netzprojekt(0.5, "KW");
        Apparat(p, "WC", "WC-1"); Apparat(p, "Waschtisch", "WT-1"); Hausanschluss(p);
        Formteil(p, "T1", "KW-1"); Formteil(p, "RD", "KW-1", "TStueck", ZweiAnschluesse());
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T1.b", "WC-1.kw");
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "T1.c", ["nach"] = "RD.a" }));
        Rohr(p, "R3", "KW-1", "RD.b", "WT-1.kw");
        Assert.DoesNotContain(Sanitaerbefunde(p), x => x.Code == "P26");
        Tw(p, "R1", 2, 0, 1, 2);
        // dasselbe hinter einer Reduktion verkehrt: T2.b → R3' → WC-2.kw; T2.a ↔ RD2.a; R4 von RD2.b nach T1-Abgang (verkehrt) – die Gruppe T2/RD2 hat keine Fortsetzung
        var q = Netzprojekt(0.5, "KW");
        Apparat(q, "WC", "WC-1"); Hausanschluss(q);
        Formteil(q, "T1", "KW-1"); Formteil(q, "T2", "KW-1"); Formteil(q, "RD2", "KW-1", "TStueck", ZweiAnschluesse());
        Rohr(q, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(q, "R2", "KW-1", "RD2.b", "T1.b");   // verkehrt
        Ok(q.Ausfuehren("verbinden", new JsonObject { ["von"] = "T2.a", ["nach"] = "RD2.a" }));
        Rohr(q, "R3", "KW-1", "T2.b", "WC-1.kw");
        var p26 = Assert.Single(Sanitaerbefunde(q), x => x.Code == "P26");
        Assert.Contains("der Weg endet bei RD2 (RD2.b ist mit R2.anfang «rein» verbunden – Fliessrichtung verkehrt)", p26.Text);
        Tw(q, "R1", 0, 0, 0, 0);
    }

    [Fact]
    public void OhneWeg_BehobenMeldetDerBefehl()
    {
        var p = VerkehrteLeitung();
        Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "R2" }));   // offenes Ende: kein P26 (Netz noch nicht fertig, P01)
        Assert.DoesNotContain(Sanitaerbefunde(p), x => x.Code == "P26");
        Assert.Contains(Pruefungen.Alle(p.Modell, p.Katalog), x => x.Code == "P01" && x.Element == "T2");
        Ok(p.Rueckgaengig());
        Assert.Contains(Sanitaerbefunde(p), x => x.Code == "P26");
        var e = Ok(p.Ausfuehren("element_loeschen", new JsonObject { ["element"] = "R2" }));
        Assert.Contains(e.BehobeneBefunde, x => x.Code == "P26" && x.Element == "WC-1");
    }

    [Fact]
    public void OhneWeg_Abwasser_VerkehrteLeitung_BelastungFehltStromab()
    {
        // WC-1.sw → S1 → T1.a; T1.b ↔ S2.ende (S2 verkehrt, von T2.a nach T1.b); T2.b → S3 → KS-1. Stromab ab T1: S2.ende «raus» – der Weg endet.
        var p = Netzprojekt(0.5, "SW");
        Apparat(p, "WC", "WC-1"); Kontrollschacht(p);
        Formteil(p, "T1", "SW-1"); Formteil(p, "T2", "SW-1");
        Rohr(p, "S1", "SW-1", "WC-1.sw", "T1.a");
        Rohr(p, "S2", "SW-1", "T2.a", "T1.b");   // verkehrt
        Rohr(p, "S3", "SW-1", "T2.b", "KS-1.ein");
        var p26 = Assert.Single(Sanitaerbefunde(p), x => x.Code == "P26");
        Assert.Equal("WC-1", p26.Element);
        Assert.Equal("Apparat ohne Weg im Netz – Belastung fehlt stromab: sw (SW) ist verbunden, der Weg endet bei T1 (T1.b ist mit S2.ende «raus» verbunden – Fliessrichtung verkehrt)", p26.Text);
        Aw(p, "S1", 2, 1);
        Aw(p, "S2", 0, 0);
        Aw(p, "S3", 0, 0);
        Assert.Equal("stromab", Abfrage(p, "sanitaer_belastung")["ohneWeg"]![0]!["fehlt"]!.GetValue<string>());
        var z = Abfrage(p, "sanitaer_zusammenstellung", new JsonObject { ["anlage"] = "A-1" });
        Assert.Contains(z["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("ΣDU enthält 1 Apparat ohne Weg im SW-Netz: WC-1 – in den Netzsummen fehlen sie stromab (Befund P26)"));
    }

    [Fact]
    public void OhneWeg_AnschlussMitFalscherRichtungOderOhneMedium()
    {
        // Anschlüsse wie aus einer Familie, direkt an einem Verteiler (ungerichtet): leitung_ziehen würde «raus» ↔ «raus» ablehnen, verbinden am Formteil nicht
        var p = Netzprojekt(0.5, "KW");
        Hausanschluss(p); Verteiler(p, "VT", "KW-1", 2);
        Rohr(p, "K0", "KW-1", "HA-1.aus", "VT.a");
        // Apparat mit kw «raus»: der Weg kann nicht stromauf gehen
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Waschtisch", ["kennzeichen"] = "WT-R", ["geschoss"] = "EG",
            ["anschluesse"] = new JsonArray(new JsonObject { ["name"] = "kw", ["medium"] = "KW", ["fluss"] = "raus" }),
        }));
        // Apparat mit Anschluss ohne Medium («*»): zählt in keiner Spalte
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Waschtisch", ["kennzeichen"] = "WT-S", ["geschoss"] = "EG",
            ["anschluesse"] = new JsonArray(new JsonObject { ["name"] = "kw", ["medium"] = "*", ["fluss"] = "rein" }),
        }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "VT.v1", ["nach"] = "WT-R.kw" }));
        Ok(p.Ausfuehren("verbinden", new JsonObject { ["von"] = "VT.v2", ["nach"] = "WT-S.kw" }));
        Tw(p, "K0", 0, 0, 0, 0);
        var b = Sanitaerbefunde(p).Where(x => x.Code == "P26").ToDictionary(x => x.Element);
        Assert.Contains("WT-R.kw hat die Fliessrichtung «raus»", b["WT-R"].Text);
        Assert.StartsWith("Apparat ohne Weg im Netz – Belastung fehlt stromauf: kw (KW)", b["WT-S"].Text);
        Assert.Contains("kein Medium («*»)", b["WT-S"].Text);
        Assert.Equal(2, Abfrage(p, "sanitaer_belastung")["ohneWeg"]!.AsArray().Count);
    }

    [Fact]
    public void OhneWeg_KeinBefund_BeiOffenemEnde_DirektAmGeraet_UndSeitenast()
    {
        // offenes Ende (Netz noch nicht fertig): kein P26, die gezeichneten Leitungen tragen den Apparat
        var p = Netzprojekt(0.5, "KW");
        Apparat(p, "WC", "WC-1");
        Rohr(p, "R1", "KW-1", null, "WC-1.kw");
        Tw(p, "R1", 1, 0, 1, 1);
        Assert.DoesNotContain(Sanitaerbefunde(p), x => x.Code == "P26");
        // Apparat direkt am Hausanschluss (ohne Leitung): kein Beitrag, kein Befund
        var q = Netzprojekt(0.5, "KW");
        Apparat(q, "WC", "WC-1"); Hausanschluss(q);
        Ok(q.Ausfuehren("verbinden", new JsonObject { ["von"] = "HA-1.aus", ["nach"] = "WC-1.kw" }));
        Assert.DoesNotContain(Sanitaerbefunde(q), x => x.Code is "P26" or "P22");
        Assert.Null(Abfrage(q, "sanitaer_belastung")["ohneWeg"]);
        // Verzweigung mit Seitenästen (Verzweigungstest): kein P26
        Assert.DoesNotContain(Sanitaerbefunde(Verzweigung()), x => x.Code == "P26");
    }

    [Fact]
    public void Masche_Abwasser_ApparatZaehltEinmal()
    {
        // WC-1.sw → S1 → T1.a; T1.b → S2 → T2.a; T1.c → S3 → T2.c; T2.b → S4 → KS-1
        var p = Netzprojekt(0.5, "SW");
        Apparat(p, "WC", "WC-1"); Kontrollschacht(p);
        Formteil(p, "T1", "SW-1"); Formteil(p, "T2", "SW-1");
        Rohr(p, "S1", "SW-1", "WC-1.sw", "T1.a");
        Rohr(p, "S2", "SW-1", "T1.b", "T2.a");
        Rohr(p, "S3", "SW-1", "T1.c", "T2.c");
        Rohr(p, "S4", "SW-1", "T2.b", "KS-1.ein");
        Aw(p, "S4", 2, 1);
        Assert.Single(Sanitaerbefunde(p), b => b.Code == "P24" && b.Element == "T2");
    }

    [Fact]
    public void Wassererwaermer_KaltwasserTraegtWarmwasser_ZirkulationAusgenommen()
    {
        // KW: HA-1 → R1 → T1; T1.b → R2 → WE-1.kw; T1.c → R3 → WT-1.kw. WW: WE-1.ww → R4 → T2; T2.b → R5 → WT-1.ww; T2.c → R6 → BW-1.ww.
        // ZIR: Z1 → WE-1.zir (Zirkulation, ausgenommen)
        var p = Netzprojekt(0.5, "KW", "WW", "ZIR");
        Apparat(p, "Waschtisch", "WT-1"); Apparat(p, "Badewanne", "BW-1");
        Hausanschluss(p); Wassererwaermer(p);
        Formteil(p, "T1", "KW-1"); Formteil(p, "T2", "WW-1");
        Rohr(p, "R1", "KW-1", "HA-1.aus", "T1.a");
        Rohr(p, "R2", "KW-1", "T1.b", "WE-1.kw");
        Rohr(p, "R3", "KW-1", "T1.c", "WT-1.kw");
        Rohr(p, "R4", "WW-1", "WE-1.ww", "T2.a");
        Rohr(p, "R5", "WW-1", "T2.b", "WT-1.ww");
        Rohr(p, "R6", "WW-1", "T2.c", "BW-1.ww");
        Rohr(p, "Z1", "ZIR-1", null, "WE-1.zir");
        Tw(p, "R1", 1, 1 + 3, 3, 2);   // KW: WT; WW über WE-1: WT + BW
        Tw(p, "R2", 0, 4, 3, 2);
        Tw(p, "R3", 1, 0, 1, 1);
        Tw(p, "R4", 0, 4, 3, 2);
        Tw(p, "R6", 0, 3, 3, 1);
        var z = Belastung(p, "Z1");
        Assert.Contains("ausgenommen", z["ausgenommen"]!.GetValue<string>());
        Assert.Null(z["summeLuWw"]);
        var a = Abfrage(p, "sanitaer_belastung")["anlagen"]![0]!;
        Assert.DoesNotContain(a["systeme"]!.AsArray(), s => s!["system"]!.GetValue<string>() == "ZIR");
        Assert.DoesNotContain(Sanitaerbefunde(p), b => b.Code == "P24");
        // BW-1.kw ist offen, das Projekt hat KW: P22 «nicht verbunden»
        Assert.Contains(Sanitaerbefunde(p), b => b.Code == "P22" && b.Element == "BW-1" && b.Text.Contains("KW-Anschluss ist aber nicht verbunden"));
    }

    [Fact]
    public void Regenwasser_Ausgenommen()
    {
        var p = Netzprojekt(0.5, "SW", "RW");
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Dachablauf", ["kennzeichen"] = "DAB-1", ["geschoss"] = "OG" }));
        Rohr(p, "RW-R1", "RW-1", "DAB-1.rw", null);
        var b = Belastung(p, "RW-R1");
        Assert.NotNull(b["ausgenommen"]);
        Assert.Null(b["summeDu"]);
        var a = Abfrage(p, "sanitaer_belastung")["anlagen"]![0]!;
        Assert.Equal(new[] { "SW" }, a["systeme"]!.AsArray().Select(s => s!["system"]!.GetValue<string>()));
        Assert.Throws<HlksFehler>(() => Abfrage(p, "sanitaer_belastung", new JsonObject { ["strang"] = "RW-1" }));
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "warum", new JsonObject { ["element"] = "RW-R1", ["eigenschaft"] = "summeDu" }));
    }

    [Fact]
    public void FehlendeArt_ZaehltNicht_OhneWert_P21()
    {
        var p = Netzprojekt(0.5, "SW");
        Apparat(p, "Bodenablauf", "BA-1"); Apparat(p, "WC", "WC-1"); Kontrollschacht(p);
        Formteil(p, "T1", "SW-1");
        Rohr(p, "S1", "SW-1", "BA-1.sw", "T1.c");
        Rohr(p, "S2", "SW-1", "WC-1.sw", "T1.a");
        Rohr(p, "S3", "SW-1", "T1.b", "KS-1.ein");
        var s3 = Belastung(p, "S3");
        Etwa(2, Zahl(s3["summeDu"]), "S3 ohne Bodenablauf");
        Assert.Equal(2, s3["apparate"]!.GetValue<int>());
        Assert.Equal(new[] { "BA-1.sw (ohne Apparateart)" }, s3["ohneWert"]!.AsArray().Select(x => x!.GetValue<string>()));
        var p21 = Assert.Single(Sanitaerbefunde(p), b => b.Code == "P21");
        Assert.Equal(("warnung", "BA-1"), (p21.Schwere, p21.Element));
        Assert.Contains("Art wählen", p21.Text);
        Assert.Contains("sanitaer_katalog", p21.Vorschlag);
        // Art wählen → Befund behoben, Summe nachgeführt (keine veraltete Zwischenspeicherung)
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BA-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "bodenablauf_50" } }));
        Assert.Contains(e.BehobeneBefunde, b => b.Code == "P21");
        Etwa(2.8, Zahl(Belastung(p, "S3")["summeDu"]), "S3 mit Bodenablauf DN 50");
    }

    [Fact]
    public void Widerspruch_AnschlussUndArt_P22()
    {
        var p = Netzprojekt(0.5, "KW", "WW", "SW");
        Apparat(p, "WC", "WC-1", "waschtisch");            // Art mit LU WW, das WC hat keinen WW-Anschluss
        Apparat(p, "Waschtisch", "WT-1", "wc_75");          // Art ohne LU WW, WW-Anschluss verbunden
        Apparat(p, "Waschtisch", "WT-2");                   // Vorgabe waschtisch, WW offen (Projekt hat WW)
        Hausanschluss(p); Wassererwaermer(p); Kontrollschacht(p);
        Formteil(p, "VT-KW", "KW-1", "TStueck", new JsonArray(new[] { "a", "v1", "v2", "v3", "v4" }.Select(x => (JsonNode)new JsonObject { ["name"] = x, ["medium"] = "*", ["fluss"] = "beide" }).ToArray()));
        Rohr(p, "K0", "KW-1", "HA-1.aus", "VT-KW.a");
        Rohr(p, "K1", "KW-1", "VT-KW.v1", "WC-1.kw");
        Rohr(p, "K2", "KW-1", "VT-KW.v2", "WT-1.kw");
        Rohr(p, "K3", "KW-1", "VT-KW.v3", "WT-2.kw");
        Rohr(p, "K4", "KW-1", "VT-KW.v4", "WE-1.kw");
        Rohr(p, "W1", "WW-1", "WE-1.ww", "WT-1.ww");
        var b = Sanitaerbefunde(p).Where(x => x.Code == "P22").ToList();
        Assert.Contains(b, x => x.Element == "WC-1" && x.Text.Contains("keinen WW-Anschluss"));
        Assert.Contains(b, x => x.Element == "WT-1" && x.Text.Contains("ww (WW) ist verbunden") && x.Text.Contains("keinen Wert LU WW"));
        Assert.Contains(b, x => x.Element == "WT-2" && x.Text.Contains("WW-Anschluss ist aber nicht verbunden"));
        Assert.All(b, x => Assert.Equal("warnung", x.Schwere));
        // SW: alle drei haben DU, die SW-Anschlüsse sind offen, das Projekt hat SW
        Assert.Equal(3, b.Count(x => x.Text.Contains("SW-Anschluss ist aber nicht verbunden")));
        // W1 zählt WT-1 ohne Wert (Art wc_75 hat kein LU WW)
        Assert.Contains("WT-1.ww", Belastung(p, "W1")["ohneWert"]![0]!.GetValue<string>());
        // nicht verbunden zählt nur, wenn der Apparat am Netz hängt und das Projekt das Medium hat
        var q = Netzprojekt(0.5, "KW");
        Apparat(q, "Waschtisch", "WT-3");
        Assert.DoesNotContain(Sanitaerbefunde(q), x => x.Code == "P22");   // nicht am Netz: nur P23
        Assert.Contains(Sanitaerbefunde(q), x => x.Code == "P23" && x.Element == "WT-3" && x.Schwere == "hinweis");
        Hausanschluss(q);
        Rohr(q, "K1", "KW-1", "HA-1.aus", "WT-3.kw");
        Assert.DoesNotContain(Sanitaerbefunde(q), x => x.Code == "P22");   // WW und SW gibt es im Projekt nicht (wie P01)
        Assert.DoesNotContain(Sanitaerbefunde(q), x => x.Code == "P23");
    }

    [Fact]
    public void ApparatInKeinerAnlage_P23()
    {
        var p = Neu();
        SystemMitStrang(p, "KW", null);   // System ohne Anlage
        Apparat(p, "WC", "WC-1"); Hausanschluss(p);
        Rohr(p, "R1", "KW-1", "HA-1.aus", "WC-1.kw");
        var p23 = Assert.Single(Sanitaerbefunde(p), b => b.Code == "P23");
        Assert.Equal(("hinweis", "WC-1"), (p23.Schwere, p23.Element));
        Tw(p, "R1", 1, 0, 1, 1);   // Summen gibt es trotzdem je Leitung
        var alle = Abfrage(p, "sanitaer_belastung");
        Assert.Empty(alle["anlagen"]!.AsArray());
        Assert.Equal("KW", alle["systemeOhneAnlage"]![0]!["system"]!.GetValue<string>());
        // System einer Anlage zuordnen → behoben
        Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Anlage", ["kennzeichen"] = "A-1" }));
        var e = Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "KW", ["teilVon"] = "A-1" }));
        Assert.Contains(e.BehobeneBefunde, b => b.Code == "P23");
    }

    [Fact]
    public void Leitungsabfrage_FormteilOderHeizung_VerstaendlicheAntwort()
    {
        var p = Verzweigung();
        var f = Assert.Throws<HlksFehler>(() => Belastung(p, "T1"));
        Assert.Contains("keine Leitung", f.Message);
        Assert.NotNull(f.Vorschlag);
        Assert.Throws<HlksFehler>(() => Belastung(p, "XYZ"));
        SystemMitStrang(p, "HZ-VL", "A-1");
        Rohr(p, "H1", "HZ-VL-1", null, null);
        Assert.NotNull(Belastung(p, "H1")["ausgenommen"]);
        Assert.DoesNotContain(Abfrage(p, "sanitaer_belastung")["anlagen"]![0]!["systeme"]!.AsArray(), s => s!["system"]!.GetValue<string>() == "HZ-VL");
    }

    [Fact]
    public void ZwischenspeicherFolgtJederAenderung_TrockenlaufAendertNichts()
    {
        var p = Verzweigung();
        Tw(p, "R1", 5, 0, 3, 3);
        var t = p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BW-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "entnahme_garten" } }, trockenlauf: true);
        Assert.True(t.Angenommen);
        Assert.Contains(t.NeueBefunde, b => b.Code == "P22" && b.Element == "BW-1");   // Entnahmearmatur ohne DU, SW verbunden
        Tw(p, "R1", 5, 0, 3, 3);   // Trockenlauf: nichts geändert
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "BW-1", ["eigenschaften"] = new JsonObject { ["sanitaerArt"] = "entnahme_garten" } }));
        Tw(p, "R1", 1 + 1 + 5, 0, 5, 3);
        Ok(p.Rueckgaengig());
        Tw(p, "R1", 5, 0, 3, 3);
        Ok(p.Ausfuehren("trennen", new JsonObject { ["anschluss"] = "R5.ende" }));
        Tw(p, "R1", 2, 0, 1, 2);
        using (Modell.PruefsummeNachpruefen()) _ = p.Modell.Pruefsumme();   // Rechnung ändert das Modell nicht
    }
}
