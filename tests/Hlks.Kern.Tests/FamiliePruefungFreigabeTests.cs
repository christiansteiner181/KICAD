using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B4: Freigabe-Regeln (nur Mensch, ohne Fehler, ohne offene Konflikte, ohne fehlende Pflichtquellen, Masse
/// innerhalb; Werte aus Bildern erlaubt und gezählt) und familie_speichern (nur freigegeben, Verweis auf Unterlagen ohne Befunde,
/// Versionsregel). Bibliotheksdateien nur in einem Temp-Ordner.</summary>
public sealed class FamiliePruefungFreigabeTests : IDisposable
{
    readonly Ordner o = new();
    readonly string bibliothek = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hlks_b4_bib_" + Guid.NewGuid().ToString("N")[..8])).FullName;

    public void Dispose()
    {
        o.Dispose();
        try { Directory.Delete(bibliothek, true); } catch (IOException) { }
    }

    [Fact]
    public void Nur_ein_Mensch_gibt_frei_und_speichert()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        var ki = Als(p, "ki");
        var f = Assert.Throws<HlksFehler>(() => new FamilieFreigeben().Ausfuehren(ki, new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Contains("nur ein Mensch", f.Message);
        Assert.Contains("KI", f.Message);
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.Contains("nur ein Mensch", Assert.Throws<HlksFehler>(() => new FamilieSpeichern().Ausfuehren(Als(p, "ki"), new JsonObject { ["familie"] = "FAM-1" })).Message);
        Assert.Contains("nur ein Mensch", Assert.Throws<HlksFehler>(() => new KonfliktEntscheiden().Ausfuehren(Als(p, "beispiel"), new JsonObject { ["familie"] = "FAM-1", ["konflikt"] = "k-x", ["wert"] = "1" })).Message);
    }

    [Fact]
    public void Freigabe_mit_Werten_aus_Bildern_nennt_ihre_Anzahl_Aenderung_setzt_entwurf()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var bild = Ablegen(p, Bild(o));
        var d = Ventil(pdf);
        TypWert(d, "V-15", "H", 35, Id(bild, "bild"));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        // Trockenlauf ändert nichts
        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }, trockenlauf: true));
        Assert.Equal("entwurf", Hole(p, "FAM-1").Text("status"));
        var e = Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        var fam = Hole(p, "FAM-1");
        Assert.Equal("freigegeben", fam.Text("status"));
        var fg = (JsonObject)fam.Eigenschaft("freigabe")!.Inhalt!;
        Assert.Equal(1, fg["ausBild"]!.GetValue<int>());
        Assert.Equal(8, fg["geprueft"]!.GetValue<int>());
        Assert.Equal(1, fg["mensch"]!.GetValue<int>());
        Assert.Equal("Christian", fg["akteur"]!.GetValue<string>());
        Assert.Equal(1, Json.Zahl(fg["version"]));
        Assert.Contains(e.Hinweise, h => h.Contains("1 aus Bildern gelesen (ungeprüft)"));
        Assert.Equal("freigegeben", Definition(p)["status"]!.GetValue<string>());
        Assert.Equal(1, Hole(p, "FAM-1").Zahl("version"));   // die Freigabe ändert die Version nicht
        // zweites Freigeben: nur Hinweis
        Assert.Contains(Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" })).Hinweise, h => h.Contains("schon freigegeben"));
        // jede Änderung setzt wieder auf entwurf (B1) und nimmt die Freigabe weg
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "neu" }) }));
        Assert.Equal("entwurf", Hole(p, "FAM-1").Text("status"));
        Assert.Null(Hole(p, "FAM-1").Eigenschaft("freigabe"));
        // rückgängig der Änderung: wieder freigegeben
        Ok(p.Rueckgaengig());
        Assert.Equal("freigegeben", Hole(p, "FAM-1").Text("status"));
    }

    [Fact]
    public void Freigabe_gesperrt_bei_fehlender_Pflichtquelle_und_bei_Typfehler()
    {
        var ki = ProjektB4("ki");
        var pdf = Ablegen(ki, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);                     // B = 30 ohne Quelle, von der KI
        TypWert(d, "V-25", "H", null);            // fehlt ganz
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var f = Assert.Throws<HlksFehler>(() => new FamilieFreigeben().Ausfuehren(Als(ki, "mensch"), new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Contains("abdeckung «B»: Pflichtwert ohne Quelle", f.Message);
        Assert.Contains("abdeckung «V-25/H»", f.Message);
        Assert.Contains("typ «V-25»", f.Message);   // H fehlt: der Typ lässt sich nicht auswerten
        Assert.Contains("Quelle (Befund-Id) nennen", f.Vorschlag);
    }

    [Fact]
    public void Entfallender_Typwert_mit_Beleg_ist_kein_fehlender_Pflichtwert()
    {
        // Befund FAM-5 Mapress Edelstahl Bogen 30.10.2026: d12/d18 gibt es nur in 45° und 90° – die übrigen Winkelmasse entfallen
        var ki = ProjektB4("ki");
        var pdf = Ablegen(ki, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        TypWert(d, "V-25", "H", null);
        var typ = (JsonObject)((JsonArray)d["typen"]!).Single(x => x!["name"]!.GetValue<string>() == "V-25")!;
        typ["entfaellt"] = new JsonObject { ["H"] = new JsonArray(Id(pdf, "zeile")) };
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var f = Assert.Throws<HlksFehler>(() => new FamilieFreigeben().Ausfuehren(Als(ki, "mensch"), new JsonObject { ["familie"] = "FAM-1" }));
        Assert.DoesNotContain("abdeckung «V-25/H»", f.Message);
        // ohne Beleg bzw. neben einem Wert: abgelehnt
        typ["entfaellt"] = new JsonObject { ["H"] = new JsonArray() };
        Assert.Contains("braucht einen Beleg", Nein(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() })).Fehler);
        TypWert(d, "V-25", "H", 40);
        typ = (JsonObject)((JsonArray)d["typen"]!).Single(x => x!["name"]!.GetValue<string>() == "V-25")!;
        typ["entfaellt"] = new JsonObject { ["H"] = new JsonArray(Id(pdf, "zeile")) };
        Assert.Contains("kann nicht entfallen", Nein(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() })).Fehler);
    }

    [Fact]
    public void Editor_Familie_ohne_Quellen_von_einem_Menschen_ist_freigebbar()
    {
        var p = ProjektB4();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        var e = Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        Assert.Equal("freigegeben", Hole(p, "FAM-1").Text("status"));
        Assert.Null(Definition(p)["abdeckung"]);   // Editor-Familie bleibt ohne Abdeckung
        Assert.Contains(e.Hinweise, h => h.Contains("0 Wert(e) gegen die Quelle geprüft"));
        // dieselbe Familie, von der KI angelegt: Werte ohne Quelle fehlen
        var ki = ProjektB4("ki");
        Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        Assert.Contains("Pflichtwert ohne Quelle", Assert.Throws<HlksFehler>(() => new FamilieFreigeben().Ausfuehren(Als(ki, "mensch"), new JsonObject { ["familie"] = "FAM-1" })).Message);
    }

    [Fact]
    public void Speichern_nur_freigegeben_mit_Verweis_ohne_Befunde_und_Versionsregel()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        var datei = Path.Combine(bibliothek, "testventil.familie.json");
        var n = Nein(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }));
        Assert.Contains("nicht freigegeben", n.Fehler);
        Assert.Contains("familie_freigeben", n.Vorschlag);
        Assert.False(File.Exists(datei));

        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        // Trockenlauf schreibt nichts
        Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }, trockenlauf: true));
        Assert.False(File.Exists(datei));
        var e = Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }));
        Assert.True(File.Exists(datei));
        var text = File.ReadAllText(datei);
        Assert.DoesNotContain("\\u00", text);   // Umlaute lesbar
        var def = (JsonObject)JsonNode.Parse(text)!;
        Assert.Equal("freigegeben", def["status"]!.GetValue<string>());
        Assert.Equal(1, def["version"]!.GetValue<int>());
        var ul = (JsonArray)def["unterlagen"]!;
        Assert.Equal("datenblatt.pdf", ul.Single()!["datei"]!.GetValue<string>());
        Assert.Equal(64, ul.Single()!["pruefsumme"]!.GetValue<string>().Length);
        Assert.Null(def["befunde"]);
        Assert.DoesNotContain("\"wort\"", text);   // keine Befunde, nur Ids in quellen
        Assert.Contains(Zelle(pdf, 1, 3), text);
        var bib = (JsonObject)Hole(p, "FAM-1").Eigenschaft("bibliothek")!.Inhalt!;
        Assert.Equal(Path.GetFullPath(datei), bib["datei"]!.GetValue<string>());
        Assert.Equal(64, bib["pruefsumme"]!.GetValue<string>().Length);
        Assert.Contains(e.Hinweise, h => h.Contains("1 Unterlage(n) als Verweis"));

        // gleiche Version: nicht überschrieben (Hinweis)
        var vorher = File.ReadAllBytes(datei);
        var gleich = Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }));
        Assert.Contains(gleich.Hinweise, h => h.Contains("Nicht gespeichert") && h.Contains("höherer Version"));
        Assert.Equal(vorher, File.ReadAllBytes(datei));

        // höhere Version: überschrieben
        Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "Version 2" }) }));
        Assert.Contains("nicht freigegeben", Nein(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei })).Fehler);
        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }));
        var neu = (JsonObject)JsonNode.Parse(File.ReadAllText(datei))!;
        Assert.Equal(2, neu["version"]!.GetValue<int>());
        Assert.Equal("Version 2", neu["beschreibung"]!.GetValue<string>());

        // die gespeicherte Datei lädt wieder (familie_laden, B1): gleiche Familie, keine Änderung
        Assert.Empty(Ok(p.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei })).Aenderungen);

        // eine andere Familie darf die Datei nicht überschreiben
        var d2 = FamilieTestHilfe.Testheizkoerper();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d2.DeepClone() }));
        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-2" }));
        Assert.Contains("gehört zu einer anderen Familie", Nein(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-2", ["datei"] = datei })).Fehler);
    }

    [Fact]
    public void Speichern_mit_relativer_Datei_wird_abgelehnt_die_mitgelieferte_Bibliothek_ist_nur_lesbar()
    {
        // Entscheid Hauptlauf 19.10.2026 (Modul 2, C3): kein Befehl schreibt mehr nach kataloge/familien
        var p = ProjektB4();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        var mitgeliefert = Path.Combine(p.Katalog.Ordner, "familien", "c3_darf_nicht_entstehen.familie.json");
        foreach (var relativ in new[] { "c3_darf_nicht_entstehen", "c3_darf_nicht_entstehen.familie.json", @"unter\c3_darf_nicht_entstehen" })
        {
            var n = Nein(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = relativ }));
            Assert.Contains("relativer Name", n.Fehler);
            Assert.Contains("nur lesbar", n.Fehler);
            Assert.Contains("datei weglassen", n.Vorschlag);
            Assert.Contains("vollen Pfad", n.Vorschlag);
            Assert.Contains(".familie.json", n.Vorschlag);
        }
        Assert.False(File.Exists(mitgeliefert));
        // auch im Trockenlauf abgelehnt; der volle Pfad bleibt der Weg für eine Ausfuhr
        Nein(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = "x" }, trockenlauf: true));
        var voll = Path.Combine(bibliothek, "ausfuhr.familie.json");
        Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = voll }));
        Assert.True(File.Exists(voll));
    }

    [Fact]
    public void Bibliothek_aus_anderem_Projekt_Quellen_bleiben_Verweis()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        Ok(p.Ausfuehren("familie_freigeben", new JsonObject { ["familie"] = "FAM-1" }));
        var datei = Path.Combine(bibliothek, "ventil.familie.json");
        Ok(p.Ausfuehren("familie_speichern", new JsonObject { ["familie"] = "FAM-1", ["datei"] = datei }));

        // anderes Projekt ohne die Unterlage: Laden geht, die Quellen bleiben als Verweis, die Freigabe bleibt
        var q = ProjektB4();
        var e = Ok(q.Ausfuehren("familie_laden", new JsonObject { ["datei"] = datei }));
        Assert.Contains(e.Hinweise, h => h.Contains("liegt nicht im Projekt"));
        Assert.Equal("freigegeben", Hole(q, "FAM-1").Text("status"));
        Assert.Equal("geprüft", Abdeckung(q, "V-15/L")["status"]!.GetValue<string>());
        // eine erfundene Quelle wird dort trotzdem abgelehnt
        var d = (JsonObject)Definition(q).DeepClone();
        TypWert(d, "V-15", "H", 35, "fedcba987654#3");
        Assert.Contains("keine Unterlage", Nein(q.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" })).Fehler);
    }

    [Fact]
    public void Dateiname_ohne_Umlaute()
    {
        Assert.Equal("Testheizkoerper_Typ_22_fiktiv.familie.json", FamilienFreigabe.Dateiname("Testheizkörper Typ 22 (fiktiv)"));
        Assert.Equal("RVP-P_300x200.familie.json", FamilienFreigabe.Dateiname("RVP-P 300×200"));
    }
}
