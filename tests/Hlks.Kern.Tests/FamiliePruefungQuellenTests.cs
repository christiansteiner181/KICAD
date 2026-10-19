using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B4: Quellenprüfung – Zahlen und Einheiten lesen, Wert gegen Befund (passt, weicht ab, m gegen mm,
/// Dezimalkomma), Befund fehlt, Bild ungeprüft, Abdeckung mit fehlenden Werten. Synthetische Unterlagen mit bekannten Werten.</summary>
public class FamiliePruefungQuellenTests
{
    // ------------------------------------------------------------------ Zahlen und Einheiten

    [Theory]
    [InlineData("1,5", 1.5, 1, null)]
    [InlineData("0,30", 0.30, 2, null)]
    [InlineData("1'250", 1250, 0, null)]
    [InlineData("67 mm", 67, 0, "mm")]
    [InlineData("6.3 m3/h", 6.3, 1, "m³/h")]
    [InlineData("DN 15", 15, 0, null)]
    [InlineData("−12", -12, 0, null)]
    public void Zahl_lesen_mit_Dezimalkomma_Einheit_und_Stellen(string text, double wert, int stellen, string? einheit)
    {
        var z = QuellZahlen.Lesen(text)!;
        Assert.Equal(wert, z.Wert, 9);
        Assert.Equal(stellen, z.Stellen);
        Assert.Equal(einheit, z.Einheit);
    }

    [Fact]
    public void Brueche_fuer_Gewinde_und_was_keine_Zahl_ist()
    {
        Assert.Equal(1.25, QuellZahlen.Lesen("1 1/4")!.Wert, 9);
        Assert.True(QuellZahlen.Lesen("1 1/4")!.Bruch);
        Assert.Equal(0.75, QuellZahlen.Lesen("3/4")!.Wert, 9);
        Assert.Equal(1.5, QuellZahlen.Lesen("Rp 1-1/2")!.Wert, 9);
        Assert.Null(QuellZahlen.Lesen("3/4", bruch: false));
        Assert.Null(QuellZahlen.Lesen("200 x 100"));
        Assert.Null(QuellZahlen.Lesen("R2015-S1"));
        var alle = QuellZahlen.AlleLesen("Gewicht 0,30 kg, L = 67mm, Typ R2015-S1");
        Assert.Equal([0.30, 67], alle.Select(z => z.Wert));
        Assert.Equal(["kg", "mm"], alle.Select(z => z.Einheit));
    }

    [Fact]
    public void Einheiten_vereinheitlichen_und_umrechnen()
    {
        Assert.Equal("m³/h", QuellEinheiten.Normal("m3/h"));
        Assert.Equal("mm", QuellEinheiten.Normal("[mm]"));
        Assert.Equal("kW", QuellEinheiten.Normal("KW"));
        Assert.Equal(1000, QuellEinheiten.Faktor("m", "mm"));
        Assert.Equal(3.6, QuellEinheiten.Faktor("l/s", "m³/h")!.Value, 9);
        Assert.Equal(1e5, QuellEinheiten.Faktor("bar", "Pa"));
        Assert.Null(QuellEinheiten.Faktor("kg", "mm"));
        Assert.Null(QuellEinheiten.Faktor("Zoll", "mm"));   // Gewindegrösse ist keine Länge
    }

    [Fact]
    public void Text_vergleichen_Gewinde_mit_Vorsatz()
    {
        Assert.True(QuellVergleiche.TextPasst("Rp 1/2", "1/2"));
        Assert.True(QuellVergleiche.TextPasst("Rp 1 1/4", "1 1/4"));
        Assert.False(QuellVergleiche.TextPasst("Rp 1 1/4", "1/4"));      // Vorsatz mit Ziffer: nicht derselbe Text
        Assert.False(QuellVergleiche.TextPasst("Rp 1 1/4", "Rp 1/4"));   // Zahlen zählen als Wörter, auch einstellige
        Assert.False(QuellVergleiche.TextPasst("Rp 1/4", "Rp 1 1/4"));   // «1 1/4» ist ein Wort (gemischter Bruch)
        Assert.True(QuellVergleiche.TextPasst("Rp 1-1/4", "Innengewinde Rp 1 1/4"));
        Assert.True(QuellVergleiche.TextPasst("Messing", "Gehäuse aus Messing, vernickelt"));
        Assert.False(QuellVergleiche.TextPasst("Mess", "Gehäuse aus Messing"));   // nur ganze Wörter
        // Nachtrag Abnahmelauf 1: Normalisierung und «alle Wörter in der Quelle»
        Assert.True(QuellVergleiche.TextPasst("ULSA ohne Isolierung", "• | ULSA, Gehäuse ohne Isolierung"));
        Assert.True(QuellVergleiche.TextPasst("ulsa, gehäuse", "• | ULSA, Gehäuse ohne Isolierung"));   // Teilstück
        Assert.True(QuellVergleiche.TextPasst("ULSA-125", "ULSA 125"));
        Assert.False(QuellVergleiche.TextPasst("ULDA ohne Isolierung", "• | ULSA, Gehäuse ohne Isolierung"));   // echter Widerspruch
        Assert.False(QuellVergleiche.TextPasst("ULSA mit Isolierung", "• | ULSA, Gehäuse ohne Isolierung"));
        Assert.Equal("ulsa gehäuse ohne isolierung 1.5 1/2", QuellVergleiche.TextVergleich("• | ULSA, Gehäuse – ohne Isolierung; 1,5 – 1/2."));
        Assert.True(QuellVergleiche.NamePasst("L [mm]", "L"));
        Assert.True(QuellVergleiche.NamePasst("Kvs (m³/h)", "kvs"));
        Assert.False(QuellVergleiche.NamePasst("L1 [mm]", "L"));
    }

    // ------------------------------------------------------------------ Wert gegen Befund

    [Fact]
    public void Wert_passt_zur_Tabellenzelle_Abdeckung_geprueft()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Ventil(pdf) }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Quellen: 9 Wert(e) geprüft"));
        var a = Abdeckung(p, "V-15/L");
        Assert.Equal("geprüft", a["status"]!.GetValue<string>());
        Assert.False(a["fehlt"]!.GetValue<bool>());
        Assert.Equal(Zelle(pdf, 1, 3), a["quellen"]![0]!.GetValue<string>());
        // B (Vorgabe 30, ohne Quelle) hat ein Mensch gesetzt
        Assert.Equal("mensch", Abdeckung(p, "B")["status"]!.GetValue<string>());
        // unterlagen als Verweis: Datei und Prüfsumme (64 Zeichen), keine Befunde
        var ul = (JsonArray)Definition(p)["unterlagen"]!;
        Assert.Single(ul);
        Assert.Equal("datenblatt.pdf", ul[0]!["datei"]!.GetValue<string>());
        Assert.Equal(64, ul[0]!["pruefsumme"]!.GetValue<string>().Length);
        Assert.StartsWith(pdf.Pruefsumme, ul[0]!["pruefsumme"]!.GetValue<string>());
    }

    [Fact]
    public void Wert_weicht_ab_gibt_Fehler_mit_Soll_Ist_und_Befundstelle()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        TypWert(d, "V-20", "L", 80, Zelle(pdf, 2, 3));   // Tabelle sagt 78
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Contains("Wert weicht von der genannten Quelle ab", n.Fehler);
        Assert.Contains("Soll (Quelle) 78", n.Fehler);
        Assert.Contains("Ist (Familie) 80 mm", n.Fehler);
        Assert.Contains("datenblatt.pdf, Seite 1", n.Fehler);
        Assert.Contains("typ «V-20».werte.L", n.Fehler);
        Assert.Contains("Konflikt", n.Vorschlag);   // Weg bei zwei Quellen
        Assert.Empty(p.Modell.DerKlasse("Familie"));
        // innerhalb der Toleranz max(1 mm; 0.5 %): 78.9 passt zu 78
        TypWert(d, "V-20", "L", 78.9, Zelle(pdf, 2, 3));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
    }

    [Fact]
    public void DN_ist_genau_und_einheitenlos()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);   // Kopf «DN [mm]»: die Einheit zählt für DN nicht
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }, trockenlauf: true));
        TypWert(d, "V-15", "dn", 15.5, Zelle(pdf, 1, 2));
        Assert.Contains("DN genau", Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() })).Fehler);
    }

    [Fact]
    public void Einheit_m_gegen_mm_und_Dezimalkomma_Kennwert_auf_die_Stellen()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var csv = Ablegen(p, Csv(o));
        var d = Ventil(pdf);
        ((JsonArray)d["parameter"]!).Add(new JsonObject { ["name"] = "kvs", ["art"] = "typ", ["datentyp"] = "zahl", ["einheit"] = "m3/h", ["gruppe"] = "kennwert", ["ziel"] = "kvs" });
        // L = 67 mm: PDF 67 [mm] und CSV 0,067 [m] – gleiche Länge, kein Konflikt
        TypWert(d, "V-15", "L", 67, Zelle(pdf, 1, 3), CsvZelle(csv, 1, 2));
        TypWert(d, "V-15", "kvs", 6.3, CsvZelle(csv, 1, 3));    // «6,3» m³/h
        TypWert(d, "V-20", "kvs", 8.5, CsvZelle(csv, 2, 3));
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Empty((JsonArray?)Definition(p)["konflikte"] ?? []);
        Assert.Equal("geprüft", Abdeckung(p, "V-15/L")["status"]!.GetValue<string>());
        Assert.Equal(2, Abdeckung(p, "V-15/L")["quellen"]!.AsArray().Count);
        Assert.Equal("geprüft", Abdeckung(p, "V-15/kvs")["status"]!.GetValue<string>());
        Assert.True(Abdeckung(p, "V-25/kvs")["fehlt"]!.GetValue<bool>());   // V-25 hat keinen Kvs: Pflichtwert fehlt
        Assert.Contains(e.Hinweise, h => h.Contains("Pflicht: V-25/kvs"));
        // Kennwert auf die Stellen der Unterlage: 6,3 → ±0.05; 6.34 passt, 6.36 nicht
        TypWert(d, "V-15", "kvs", 6.34, CsvZelle(csv, 1, 3));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" }));
        TypWert(d, "V-15", "kvs", 6.36, CsvZelle(csv, 1, 3));
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" }));
        Assert.Contains("auf die Stellen der Unterlage = ±0.05 m³/h", n.Fehler);
        // falsche Grösse: Kvs aus der Längenspalte
        TypWert(d, "V-15", "kvs", 6.3, CsvZelle(csv, 1, 2));
        Assert.Contains("lässt sich nicht in «m³/h» umrechnen", Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" })).Fehler);
    }

    [Fact]
    public void Befund_den_es_nicht_gibt_und_Tabelle_als_Quelle_sind_Fehler()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        TypWert(d, "V-15", "H", 35, pdf.Pruefsumme + "#99999");
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Contains($"Den Befund «{pdf.Pruefsumme}#99999» gibt es nicht", n.Fehler);
        Assert.Contains("befunde", n.Vorschlag);
        TypWert(d, "V-15", "H", 35, "0123456789ab#1");
        n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Contains("keine Unterlage", n.Fehler);
        Assert.Contains("unterlage_einfuegen", n.Vorschlag);
        // auch an Körpern und Anschlüssen
        d = Ventil(pdf);
        ((JsonObject)((JsonArray)d["koerper"]!)[0]!)["quellen"] = new JsonArray(pdf.Pruefsumme + "#88888");
        Assert.Contains("koerper «gehaeuse».quellen", Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() })).Fehler);
        // eine ganze Tabelle ist keine Quelle eines Werts
        d = Ventil(pdf);
        TypWert(d, "V-15", "H", 35, pdf.Befunde.Single(x => x.Art == "tabelle" && x.Seite == 1).Id);
        Assert.Contains("keine Quelle eines einzelnen Werts", Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() })).Fehler);
    }

    [Fact]
    public void Wert_aus_Bild_ist_ungeprueft_ohne_Fehler()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var bild = Ablegen(p, Bild(o));
        var d = Ventil(pdf);
        TypWert(d, "V-15", "H", 36, Id(bild, "bild"));   // aus dem Bild gelesen – der Kern kann nicht nachrechnen
        TypWert(d, "V-20", "H", 46, Id(pdf, "mass"));    // Masslinie mit Buchstabe «L», ohne Zahl
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var a = Abdeckung(p, "V-15/H");
        Assert.Equal("ungeprüft-bild", a["status"]!.GetValue<string>());
        Assert.Equal(QuellVergleiche.AusBild, a["vermerk"]!.GetValue<string>());
        Assert.False(a["fehlt"]!.GetValue<bool>());
        Assert.Equal("ungeprüft-bild", Abdeckung(p, "V-20/H")["status"]!.GetValue<string>());
        Assert.Contains("Masslinie", Abdeckung(p, "V-20/H")["vermerk"]!.GetValue<string>());
        Assert.Contains(e.Hinweise, h => h.Contains("2 aus Bild bzw. ungeprüft"));
    }

    [Fact]
    public void Abdeckung_mit_fehlenden_Werten_KI_ohne_Quelle_ist_fehlt_Mensch_ist_mensch()
    {
        var ordner = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "hlks_b4_" + Guid.NewGuid().ToString("N")[..8])).FullName;
        try
        {
            var ki = Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner), "Test B4", "KI", "pc-ki", "ki");
            using var o = new Ordner();
            var pdf = Ablegen(ki, UnterlagenHilfe.TestPdf(o));
            var d = Ventil(pdf);
            TypWert(d, "V-25", "H", null);                    // Wert fehlt
            Ok(ki.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
            var b = Abdeckung(ki, "B");                        // Vorgabe 30 von der KI ohne Quelle
            Assert.Equal("fehlt", b["status"]!.GetValue<string>());
            Assert.True(b["fehlt"]!.GetValue<bool>());
            var h = Abdeckung(ki, "V-25/H");
            Assert.Equal("fehlt", h["status"]!.GetValue<string>());
            Assert.Equal("Wert fehlt", h["vermerk"]!.GetValue<string>());
            Assert.Null(h["wert"]);
            Assert.False(Abdeckung(ki, "V-25/L")["fehlt"]!.GetValue<bool>());
            Assert.True(ki.Freigeben());

            // ein Mensch setzt B (Editor): danach «mensch»; die unveränderten Werte behalten ihren Status
            var cs = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "Christian", "pc-cs");
            Ok(cs.Ausfuehren("familie_aendern", new JsonObject
            {
                ["familie"] = "FAM-1",
                ["ops"] = new JsonArray(new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "B", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung", ["wert"] = 32 } }),
            }));
            Assert.Equal("mensch", Abdeckung(cs, "B")["status"]!.GetValue<string>());
            Assert.Equal("fehlt", Abdeckung(cs, "V-25/H")["status"]!.GetValue<string>());
            Assert.Equal("geprüft", Abdeckung(cs, "V-15/L")["status"]!.GetValue<string>());
            // die KI ändert B wieder ohne Quelle: fehlt
            cs.Freigeben(); ki.Synchronisieren();
            Ok(ki.Ausfuehren("familie_aendern", new JsonObject
            {
                ["familie"] = "FAM-1",
                ["ops"] = new JsonArray(new JsonObject { ["op"] = "parameter_setzen", ["parameter"] = new JsonObject { ["name"] = "B", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung", ["wert"] = 31 } }),
            }));
            Assert.Equal("fehlt", Abdeckung(ki, "B")["status"]!.GetValue<string>());
            ki.Freigeben();
        }
        finally { try { Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    [Fact]
    public void Familie_ohne_Quellen_bleibt_unveraendert()
    {
        // Editor-Familie ohne Quellen: keine Abdeckung im Speicher, gleiche Definition = keine Änderung (B1)
        var p = ProjektB4();
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        Assert.Null(Definition(p)["abdeckung"]);
        Assert.Empty(Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper(), ["familie"] = "FAM-1" })).Aenderungen);
    }

    [Fact]
    public void Quellen_an_Formel_und_Parameter_ohne_Wert_sind_Belege()
    {
        using var o = new Ordner();
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        // L nennt die Spalte (Beleg, kein Wert); ein Formelparameter mit Quelle gibt einen Hinweis
        ((JsonObject)((JsonArray)d["parameter"]!)[1]!)["quellen"] = new JsonArray(Zelle(pdf, 2, 3));
        ((JsonArray)d["parameter"]!).Add(new JsonObject { ["name"] = "Hhalb", ["art"] = "typ", ["datentyp"] = "laenge", ["formel"] = "H / 2", ["quellen"] = new JsonArray(Zelle(pdf, 1, 4)) });
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Contains(e.Hinweise, h => h.Contains("parameter «Hhalb» hat eine Formel"));
        Assert.Null(Definition(p)["abdeckung"]!["Hhalb"]);
        Assert.Null(Definition(p)["abdeckung"]!["L"]);
    }
}
