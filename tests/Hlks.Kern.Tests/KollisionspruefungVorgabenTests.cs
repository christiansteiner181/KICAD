using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.KollisionspruefungHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3b, B2: Projektvorgaben (VG-1) – jeder Wert mit Quelle, keine eingebauten Werte; vorgaben_setzen lehnt Werte
/// ohne Quelle ab (alles oder nichts), Abfrage vorgaben nennt fehlende Werte mit ihrer Wirkung. Alle Werte FIKTIV.</summary>
public class KollisionspruefungVorgabenTests
{
    static BefehlsErgebnis Setzen(Projekt p, string json, bool trocken = false) =>
        p.Ausfuehren("vorgaben_setzen", new JsonObject { ["werte"] = JsonNode.Parse(json) }, trocken);

    [Fact]
    public void OhneQuelle_Abgelehnt_AllesOderNichts()
    {
        var p = Neu();
        var f = Nein(Setzen(p, """{ "abstand_leitungen": { "wert": 50 } }"""));
        Assert.Contains("abstand_leitungen: Quelle fehlt", f.Fehler);
        Assert.Contains("quelle", f.Vorschlag);
        Nein(Setzen(p, """{ "abstand_leitungen": { "wert": 50, "quelle": "  " } }"""));
        Assert.Contains("Wert ohne Quelle", Nein(Setzen(p, """{ "abstand_leitungen": 50 }""")).Fehler);
        // ein guter und ein schlechter Wert: nichts wird gesetzt, beide Fehler genannt
        var zwei = Nein(Setzen(p, """{ "toleranz_hart": { "wert": 1, "quelle": "FIKTIV" }, "abstand_leitungen": { "wert": 50 }, "gibtsnicht": { "wert": 1, "quelle": "x" } }"""));
        Assert.Contains("abstand_leitungen", zwei.Fehler);
        Assert.Contains("«gibtsnicht» gibt es nicht", zwei.Fehler);
        Assert.Contains("toleranz_hart", zwei.Fehler);                        // in der Liste der erlaubten Namen
        Assert.Empty(p.Modell.DerKlasse("Vorgaben"));
        Assert.Contains("negativ", Nein(Setzen(p, """{ "toleranz_hart": { "wert": -1, "quelle": "FIKTIV" } }""")).Fehler);
        Assert.Contains("Zahl", Nein(Setzen(p, """{ "toleranz_hart": { "wert": "viel", "quelle": "FIKTIV" } }""")).Fehler);
        Assert.Contains("Einheit", Nein(Setzen(p, """{ "toleranz_hart": { "wert": 1, "einheit": "cm", "quelle": "FIKTIV" } }""")).Fehler);
        Assert.Contains("«sanitaer-mond»", Nein(Setzen(p, """{ "abstand_weich": { "wert": { "sanitaer-mond": 10 }, "quelle": "FIKTIV" } }""")).Fehler);
        Assert.Contains("HLKS-Gewerk", Nein(Setzen(p, """{ "abstand_weich": { "wert": { "architektur-fremd": 10 }, "quelle": "FIKTIV" } }""")).Fehler);
        Assert.Contains("doppelt", Nein(Setzen(p, """{ "abstand_weich": { "wert": { "sanitaer-architektur": 10, "architektur-sanitaer": 20 }, "quelle": "FIKTIV" } }""")).Fehler);
        Nein(Setzen(p, """{}"""));
        Nein(p.Ausfuehren("vorgaben_setzen", new JsonObject()));
        Assert.Empty(p.Modell.DerKlasse("Vorgaben"));
    }

    [Fact]
    public void Setzen_LegtVg1An_AendertEs_EntferntWerte_TrockenlaufUndHerkunft()
    {
        var p = Neu();
        var tl = Ok(Setzen(p, $$"""{ "toleranz_hart": { "wert": 1, "quelle": "{{QuelleFiktiv}}" } }""", trocken: true));
        Assert.True(tl.Trockenlauf);
        Assert.Empty(p.Modell.DerKlasse("Vorgaben"));
        var e = Ok(Setzen(p, $$"""
            { "toleranz_hart": { "wert": 1, "einheit": "mm", "quelle": "{{QuelleFiktiv}}" },
              "abstand_weich": { "wert": { "Sanitaer-Architektur": 20, "hlks-fremd": 30 }, "quelle": "FIKTIV Projektfestlegung 19.10.2026" } }
            """));
        var vg = Assert.Single(p.Modell.DerKlasse("Vorgaben"));
        Assert.Equal("VG-1", vg.Kennzeichen);
        Assert.Equal(1, vg.Zahl("toleranz_hart"));
        Assert.Equal("mm", vg.Eigenschaft("toleranz_hart")!.Einheit);
        Assert.Equal(QuelleFiktiv, vg.Eigenschaft("toleranz_hart")!.Herkunft.Quelle);
        Assert.Equal("vorgaben_setzen", vg.Eigenschaft("toleranz_hart")!.Herkunft.Befehl);
        Assert.Equal("""{"architektur-sanitaer":20,"fremd-hlks":30}""", vg.Eigenschaft("abstand_weich")!.Inhalt!.ToJsonString());
        Assert.Contains("toleranz_hart = 1 mm", Hinweis(e, "VG-1:"));
        // Nachtrag B3c: abstand_leitungen ist optional (Abstandsprüfung aus) und fehlt nicht mehr
        Assert.DoesNotContain("abstand_leitungen", Hinweis(e, "Noch ohne Wert"));
        Assert.Contains("spiel_aussparung", Hinweis(e, "Noch ohne Wert"));
        // ändern: dasselbe Element, gleicher Wert und gleiche Quelle = keine Änderung
        Ok(Setzen(p, """{ "abstand_leitungen": { "wert": 50, "quelle": "FIKTIV" } }"""));
        Assert.Single(p.Modell.DerKlasse("Vorgaben"));
        Assert.Contains("Keine Änderung.", Ok(Setzen(p, """{ "abstand_leitungen": { "wert": 50, "quelle": "FIKTIV" } }""")).Hinweise);
        // null entfernt
        Ok(Setzen(p, """{ "abstand_leitungen": null }"""));
        Assert.Null(Hole(p, "VG-1").Eigenschaft("abstand_leitungen"));
        var v = Projektvorgaben.Lesen(p.Modell);
        Assert.Equal(1, v.Zahl(Projektvorgaben.ToleranzHart));
        Assert.Null(v.Zahl(Projektvorgaben.AbstandLeitungen));
        Assert.Equal(20, v.AbstaendeWeich["architektur-sanitaer"]);
        // rückgängig: Wert wieder da
        Ok(p.Rueckgaengig());
        Assert.Equal(50, Hole(p, "VG-1").Zahl("abstand_leitungen"));
    }

    [Fact]
    public void Abfrage_WerteMitQuelle_FehlendMitWirkung_OhneQuelleGiltNicht()
    {
        var p = Neu();
        var leer = (JsonObject)Tuer.Abfrage(p, "vorgaben");
        Assert.Null(leer["element"]);
        // 6 im Katalog: grenze_kernbohrung nicht mehr verwendet (Nachtrag B3b), abstand_weich und abstand_leitungen optional (Nachtrag B3c)
        Assert.Equal(["abstand_zusammenfassen", "spiel_aussparung", "toleranz_hart"], leer["fehlend"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()).Order());
        Assert.DoesNotContain("grenze_kernbohrung", leer["fehlend"]!.ToJsonString());
        Assert.Contains("vorgaben_setzen", leer["hinweise"]!.ToJsonString());
        Ok(Vorgaben(p, (Projektvorgaben.AbstandLeitungen, 50)));
        // von Hand über element_aendern ohne Quelle: gilt nicht
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "VG-1", ["eigenschaften"] = new JsonObject { ["toleranz_hart"] = 2 } }));
        var o = (JsonObject)Tuer.Abfrage(p, "vorgaben");
        Assert.Equal("VG-1", o["element"]!.GetValue<string>());
        Assert.Equal(50, o["werte"]!["abstand_leitungen"]!["wert"]!.GetValue<double>());
        Assert.Equal(QuelleFiktiv, o["werte"]!["abstand_leitungen"]!["quelle"]!.GetValue<string>());
        Assert.Null(o["werte"]!["toleranz_hart"]);
        Assert.Equal(["toleranz_hart"], o["ohneQuelle"]!.AsArray().Select(x => x!.GetValue<string>()));
        var fehlend = o["fehlend"]!.AsArray().Select(x => x!["name"]!.GetValue<string>()).ToList();
        Assert.Contains("toleranz_hart", fehlend);
        Assert.DoesNotContain("abstand_leitungen", fehlend);
        Assert.Contains("jede Durchdringung", o["fehlend"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "toleranz_hart")!["wirkung"]!.GetValue<string>());
        Assert.Contains("architektur", o["abstandWeichSeiten"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Null(Projektvorgaben.Lesen(p.Modell).Zahl(Projektvorgaben.ToleranzHart));
        // die Prüfung nennt toleranz_hart als fehlend
        Assert.Contains("jede Durchdringung", Hinweis(Ok(Pruefen(p)), "toleranz_hart fehlt"));
    }

    /// <summary>Nachtrag B3b (Entscheid Christian 19.10.2026, Kernbohrungen nur von Hand): grenze_kernbohrung verlangt kein Befehl mehr.
    /// vorgaben_setzen nimmt sie mit Hinweis an (kein Bruch alter Projekte), die Abfrage nennt sie «nicht mehr verwendet» und nicht
    /// unter «fehlend»; «Noch ohne Wert» nennt sie nicht.</summary>
    [Fact]
    public void GrenzeKernbohrung_NichtMehrVerwendet_AngenommenMitHinweis_AbfrageNenntSie()
    {
        var p = Neu();
        var leer = (JsonObject)Tuer.Abfrage(p, "vorgaben");
        var nmv = Assert.Single(leer["nichtMehrVerwendet"]!.AsArray())!;
        Assert.Equal("grenze_kernbohrung", nmv["name"]!.GetValue<string>());
        Assert.False(nmv["gesetzt"]!.GetValue<bool>());
        Assert.Contains("nicht mehr verwendet (Entscheid Christian 19.10.2026)", nmv["hinweis"]!.GetValue<string>());

        var e = Ok(Setzen(p, """{ "grenze_kernbohrung": { "wert": 150, "quelle": "FIKTIV altes Projekt" }, "abstand_leitungen": { "wert": 50, "quelle": "FIKTIV" } }"""));
        Assert.Equal(150, Hole(p, "VG-1").Zahl("grenze_kernbohrung"));   // gespeichert und lesbar
        Assert.Contains(e.Hinweise, h => h.StartsWith("grenze_kernbohrung: nicht mehr verwendet (Entscheid Christian 19.10.2026)") && h.Contains("aussparung_form"));
        Assert.DoesNotContain("grenze_kernbohrung", Hinweis(e, "Noch ohne Wert"));
        // nochmals derselbe Wert: keine Änderung, Hinweis trotzdem
        var z = Ok(Setzen(p, """{ "grenze_kernbohrung": { "wert": 150, "quelle": "FIKTIV altes Projekt" } }"""));
        Assert.Contains("Keine Änderung.", z.Hinweise);
        Assert.Contains(z.Hinweise, h => h.StartsWith("grenze_kernbohrung: nicht mehr verwendet"));

        var o = (JsonObject)Tuer.Abfrage(p, "vorgaben");
        var w = o["werte"]!["grenze_kernbohrung"]!;
        Assert.Equal(150, w["wert"]!.GetValue<double>());
        Assert.False(w["verwendet"]!.GetValue<bool>());
        Assert.Contains("nicht mehr verwendet (Entscheid Christian 19.10.2026)", w["hinweis"]!.GetValue<string>());
        Assert.True(o["nichtMehrVerwendet"]![0]!["gesetzt"]!.GetValue<bool>());
        Assert.Contains(o["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("grenze_kernbohrung ist gesetzt, wird aber nicht mehr verwendet"));
        Assert.Null(o["werte"]!["abstand_leitungen"]!["verwendet"]);   // nur die nicht mehr verwendeten tragen das Feld
        Assert.Contains("nicht mehr verwendet", Projektvorgaben.Wirkung[Projektvorgaben.GrenzeKernbohrung]);
        // entfernen geht weiterhin
        Ok(Setzen(p, """{ "grenze_kernbohrung": null }"""));
        Assert.Null(Hole(p, "VG-1").Eigenschaft("grenze_kernbohrung"));
    }
}
