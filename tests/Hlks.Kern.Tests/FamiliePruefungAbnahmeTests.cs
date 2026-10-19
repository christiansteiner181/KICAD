using System.Text.Json.Nodes;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.FamiliePruefungHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 2, B4: Befunde aus Abnahmelauf 1 (FläktGroup Optivent Ultra, Entscheide Hauptlauf 18.10.2026), nachgestellt mit
/// synthetischen Unterlagen und FIKTIVEN Werten: Massvergleich mit Rückfall auf den gleichnamigen Typparameter und Hinweis «ohne
/// Sollwert» schon im Trockenlauf; Textwerte nach Normalisierung und Wortvergleich; Sollwert nur aus der eigenen Tabellenzeile des
/// Typs (zwei Tabellen mit gleichen Grössen auf einer Seite, wie ULSA/ULDA).</summary>
public sealed class FamiliePruefungAbnahmeTests : IDisposable
{
    readonly Ordner o = new();
    public void Dispose() => o.Dispose();

    static JsonObject Zeile(JsonObject abf, string typ, string mass) =>
        (JsonObject)((JsonArray)abf["typen"]!).Single(t => t!["typ"]!.GetValue<string>() == typ)!["abweichungen"]!.AsArray().Single(z => z!["mass"]!.GetValue<string>() == mass)!;

    // ------------------------------------------------------------------ 1. Massvergleich ohne Soll

    [Fact]
    public void Mass_ohne_parameter_nimmt_den_gleichnamigen_Typparameter_und_meldet_fehlende_Sollwerte_im_Trockenlauf()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var d = Ventil(pdf);
        // wie im Abnahmelauf: Masse mit von/bis, ohne parameter und ohne Quellen je Typ
        var masse = (JsonArray)d["masse"]!;
        ((JsonObject)masse[0]!).Remove("quellen");                                   // L
        ((JsonArray)d["parameter"]!).Add(new JsonObject { ["name"] = "D", ["art"] = "typ", ["datentyp"] = "laenge", ["gruppe"] = "abmessung", ["wert"] = 20 });
        masse.Add(new JsonObject { ["name"] = "ØD", ["ansicht"] = "vorne", ["von"] = new JsonArray(0, 0, 0), ["bis"] = new JsonArray("D", 0, 0), ["richtung"] = "x" });
        masse.Add(new JsonObject { ["name"] = "ØDy", ["ansicht"] = "vorne", ["von"] = new JsonArray(0, 0, 0), ["bis"] = new JsonArray("D + 2", 0, 0), ["richtung"] = "x" });

        var t = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }, trockenlauf: true));
        Assert.Contains(t.Hinweise, h => h == "Mass ØDy ohne Sollwert: parameter (Typparameter mit dem Tabellenwert) angeben (3 von 3 Typen).");
        Assert.DoesNotContain(t.Hinweise, h => h.StartsWith("Mass L ohne Sollwert") || h.StartsWith("Mass ØD ohne Sollwert"));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        // auch bei familie_aendern (Trockenlauf)
        var ta = Ok(p.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "kopf_setzen", ["beschreibung"] = "x" }) }, trockenlauf: true));
        Assert.Contains(ta.Hinweise, h => h.StartsWith("Mass ØDy ohne Sollwert: parameter (Typparameter mit dem Tabellenwert) angeben"));

        var abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        var l = Zeile(abf, "V-15", "L");
        Assert.Equal(67, l["soll"]!.GetValue<double>());
        Assert.StartsWith("Soll aus Parameter L (gleicher Name) – datenblatt.pdf, Seite 1", l["quelle"]!.GetValue<string>());
        Assert.Equal([Zelle(pdf, 1, 3)], l["quellen"]!.AsArray().Select(x => x!.GetValue<string>()));   // samt Quellen des Typwerts
        Assert.True(l["bestanden"]!.GetValue<bool>());
        var dd = Zeile(abf, "V-20", "ØD");
        Assert.Equal("Soll aus Parameter D (gleicher Name)", dd["quelle"]!.GetValue<string>());   // «ØD» → «D»
        Assert.Equal(20, dd["soll"]!.GetValue<double>());
        Assert.Equal(1, dd["toleranz"]!.GetValue<double>());                                  // Durchmesser
        var dy = Zeile(abf, "V-25", "ØDy");
        Assert.Null(dy["soll"]);
        Assert.Null(dy["bestanden"]);
        Assert.Equal(3, abf["ohneVergleich"]!.GetValue<int>());                               // nur ØDy, je Typ

        // mit parameter verschwindet der Hinweis
        masse[^1]!["parameter"] = "H";
        var t2 = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" }, trockenlauf: true));
        Assert.DoesNotContain(t2.Hinweise, h => h.Contains("ohne Sollwert"));
    }

    // ------------------------------------------------------------------ 2. Textwerte

    static string PdfAusfuehrung(Ordner o)
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);   // richtet die Schriften ein
            var f = new XFont("Arial", 10);
            g.DrawString("•", f, XBrushes.Black, 50, 100);
            g.DrawString("ULSA, Gehäuse ohne Isolierung", f, XBrushes.Black, 80, 100);
            g.DrawString("•", f, XBrushes.Black, 50, 130);
            g.DrawString("ULDA, Gehäuse mit Isolierung", f, XBrushes.Black, 80, 130);
        }
        var pfad = o.Datei("ausfuehrung.pdf");
        doc.Save(pfad);
        return pfad;
    }

    [Fact]
    public void Textwert_passt_nach_Normalisierung_und_Wortvergleich_echter_Widerspruch_bleibt_Fehler()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, UnterlagenHilfe.TestPdf(o));
        var aus = Ablegen(p, PdfAusfuehrung(o));
        var ulsa = aus.Befunde.First(x => x.Art == "zeile" && x.Wert!.ToString().Contains("ULSA,"));
        Assert.StartsWith("•", ulsa.Wert!.ToString());   // Aufzählungszeichen steht im Befund
        var d = Ventil(pdf);
        ((JsonArray)d["parameter"]!).Add(new JsonObject { ["name"] = "ausfuehrung", ["art"] = "typ", ["datentyp"] = "text", ["gruppe"] = "sonstiges" });
        foreach (var typ in new[] { "V-15", "V-20", "V-25" }) TypWert(d, typ, "ausfuehrung", "ULSA ohne Isolierung", ulsa.Id);
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        Assert.Equal("geprüft", Abdeckung(p, "V-20/ausfuehrung")["status"]!.GetValue<string>());

        TypWert(d, "V-20", "ausfuehrung", "ULDA ohne Isolierung", ulsa.Id);   // echter Widerspruch
        var n = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" }));
        Assert.Contains("Wert weicht von der genannten Quelle ab", n.Fehler);
        Assert.Contains("«ULDA ohne Isolierung»", n.Fehler);
        Assert.Contains("typ «V-20».werte.ausfuehrung", n.Fehler);
        Assert.DoesNotContain("V-15", n.Fehler);
    }

    // ------------------------------------------------------------------ 3. Tabellenzeile nur aus den eigenen Quellen des Typs

    /// <summary>Eine Seite mit zwei Tabellen gleicher Grössen (100, 250, 400): «ULSA» mit L 461 / 520 / 600, darunter «ULDA» mit
    /// L 900 / 925 / 950 (fiktiv).</summary>
    static string PdfZweiTabellen(Ordner o, string datei = "ulsa_ulda.pdf", string kopf = "Grösse", string[]? erste = null, string[]? zweite = null)
    {
        var doc = new PdfDocument();
        var s = doc.AddPage(); s.Width = XUnit.FromPoint(595); s.Height = XUnit.FromPoint(842);
        using (var g = XGraphics.FromPdfPage(s))
        {
            _ = new PdfZeichner(g, 297);
            var gross = new XFont("Arial", 12); var f = new XFont("Arial", 9);
            var pen = new XPen(XColors.Black, 0.5);
            void Tabelle(string titel, double y, string[] l, string[] gr)
            {
                g.DrawString(titel, gross, XBrushes.Black, 50, y - 40);
                void T(string t, double x, double yy) => g.DrawString(t, f, XBrushes.Black, x, yy);
                T(kopf, 50, y); T("L", 150, y); T("m", 220, y);
                T("[mm]", 150, y + 12); T("[kg]", 220, y + 12);
                string[] m = ["1,5", "2,1", "3,0"];
                for (int r = 0; r < 3; r++) { T(gr[r], 50, y + 30 + 14 * r); T(l[r], 150, y + 30 + 14 * r); T(m[r], 220, y + 30 + 14 * r); }
                foreach (var yy in new[] { y - 3, y + 27, y + 41, y + 55, y + 69 }) g.DrawLine(pen, 45, yy, 270, yy);
            }
            Tabelle("Optivent ULSA (fiktiv)", 150, ["461", "520", "600"], erste ?? ["100", "250", "400"]);
            Tabelle("Optivent ULDA (fiktiv)", 450, ["900", "925", "950"], zweite ?? ["100", "250", "400"]);
        }
        var pfad = o.Datei(datei);
        doc.Save(pfad);
        return pfad;
    }

    [Fact]
    public void Sollwert_nur_aus_der_eigenen_Tabellenzeile_des_Typs_bei_zwei_Tabellen_mit_gleichen_Groessen()
    {
        var p = ProjektB4();
        var pdf = Ablegen(p, PdfZweiTabellen(o));
        var tabellen = pdf.Befunde.Where(x => x.Art == "tabelle" && x.Seite == 1).OrderByDescending(x => x.Box![3]).ToList();
        Assert.Equal(2, tabellen.Count);
        string Z(int tab, int zeile, int spalte) => UnterlagenHilfe.Zelle(pdf, tabellen[tab].Id, zeile, spalte).Id;
        Assert.Equal("950", pdf.Befunde.Single(x => x.Id == Z(1, 3, 2)).Zusatz!["text"]!.ToString());

        // Typen nennen nur ihre Grösse (Spalte 1 ihrer eigenen Tabelle); L ohne Quelle; das Mass L nennt die Spalte L beider Tabellen
        JsonObject Typ(string name, int tab, int zeile, double groesse, double l) => new()
        {
            ["name"] = name, ["werte"] = new JsonObject { ["groesse"] = groesse, ["L"] = l },
            ["quellen"] = new JsonObject { ["groesse"] = new JsonArray(Z(tab, zeile, 1)) },
        };
        var d = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "VAV-Regler (fiktiv)", "kategorie": "Volumenstromregler",
              "parameter": [ { "name": "groesse", "art": "typ", "datentyp": "zahl", "gruppe": "abmessung" },
                             { "name": "L", "art": "typ", "datentyp": "laenge", "gruppe": "abmessung" } ],
              "typen": [], "koerper": [{ "id": "k", "art": "quader", "von": [0, -50, 0], "bis": ["L", 50, 100] }], "anschluesse": [] }
            """);
        d["typen"] = new JsonArray(Typ("ULSA-100", 0, 1, 100, 461), Typ("ULSA-400", 0, 3, 400, 600), Typ("ULDA-100", 1, 1, 100, 900), Typ("ULDA-400", 1, 3, 400, 950));
        d["masse"] = new JsonArray(new JsonObject
        {
            ["name"] = "L", ["ansicht"] = "vorne", ["von"] = new JsonArray(0, 0, 0), ["bis"] = new JsonArray("L", 0, 0), ["richtung"] = "x",
            ["quellen"] = new JsonArray(Z(0, 1, 2), Z(1, 1, 2)),
        });
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone() }));
        var abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        Assert.Equal(950, Zeile(abf, "ULDA-400", "L")["soll"]!.GetValue<double>());   // nicht 600 aus der ULSA-Tabelle
        Assert.Equal(Z(1, 3, 2), Zeile(abf, "ULDA-400", "L")["befund"]!.GetValue<string>());
        Assert.Equal(900, Zeile(abf, "ULDA-100", "L")["soll"]!.GetValue<double>());
        Assert.Equal(600, Zeile(abf, "ULSA-400", "L")["soll"]!.GetValue<double>());
        Assert.Equal(461, Zeile(abf, "ULSA-100", "L")["soll"]!.GetValue<double>());
        Assert.Equal(4, abf["bestanden"]!.GetValue<int>());

        // ULDA-400 nennt zusätzlich die Grösse 400 der ULSA-Tabelle: zwei Zeilen mit verschiedenen L – kein Soll, Hinweis
        var t = (JsonObject)((JsonArray)d["typen"]!)[3]!;
        t["quellen"]!["groesse"] = new JsonArray(Z(1, 3, 1), Z(0, 3, 1));
        var e = Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d.DeepClone(), ["familie"] = "FAM-1" }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Mass L: Tabellenzeile für Typ ULDA-400 nicht eindeutig"));
        abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        var z = Zeile(abf, "ULDA-400", "L");
        Assert.Null(z["soll"]);
        Assert.True(z["mehrdeutig"]!.GetValue<bool>());
        Assert.Contains(z["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Mass L: Tabellenzeile für Typ ULDA-400 nicht eindeutig"));
        Assert.Equal(900, Zeile(abf, "ULDA-100", "L")["soll"]!.GetValue<double>());   // die übrigen Typen unverändert
        // die Freigabe sperrt dieses Mass nicht (kein Soll ist kein Fehler), meldet es aber in der Prüfung
        Assert.DoesNotContain(e.Hinweise, h => h.StartsWith("Mass L ohne Sollwert"));
    }

    /// <summary>Familie ohne Quellen an den Typen (T1–T3, L 461 / 520 / 600); das Mass L nennt nur die Spalte L der ersten Tabelle.</summary>
    static JsonObject OhneTypquellen(UnterlagenBefunde pdf)
    {
        var t0 = pdf.Befunde.Where(x => x.Art == "tabelle" && x.Seite == 1).OrderByDescending(x => x.Box![3]).First();
        var d = FamilieTestHilfe.JsonAus("""
            { "format": 1, "name": "Namenprobe (fiktiv)", "kategorie": "Volumenstromregler",
              "parameter": [ { "name": "L", "art": "typ", "datentyp": "laenge", "gruppe": "abmessung" } ],
              "typen": [ { "name": "T1", "werte": { "L": 461 } }, { "name": "T2", "werte": { "L": 520 } }, { "name": "T3", "werte": { "L": 600 } } ],
              "koerper": [{ "id": "k", "art": "quader", "von": [0, -50, 0], "bis": ["L", 50, 100] }], "anschluesse": [] }
            """);
        d["masse"] = new JsonArray(new JsonObject
        {
            ["name"] = "L", ["ansicht"] = "vorne", ["von"] = new JsonArray(0, 0, 0), ["bis"] = new JsonArray("L", 0, 0), ["richtung"] = "x",
            ["quellen"] = new JsonArray(UnterlagenHilfe.Zelle(pdf, t0.Id, 1, 2).Id),
        });
        return d;
    }

    [Fact]
    public void Zeile_ueber_den_Typnamen_nur_wenn_er_in_genau_einer_Zelle_steht()
    {
        // eindeutig: T1–T3 stehen nur in der ersten Tabelle (die zweite heisst X1–X3)
        var p = ProjektB4();
        var pdf = Ablegen(p, PdfZweiTabellen(o, "namen_eindeutig.pdf", "Typ", ["T1", "T2", "T3"], ["X1", "X2", "X3"]));
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = OhneTypquellen(pdf) }));
        var abf = Abfrage(p, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        var z = Zeile(abf, "T3", "L");
        Assert.Equal(600, z["soll"]!.GetValue<double>());
        Assert.True(z["bestanden"]!.GetValue<bool>());
        Assert.Contains(z["hinweise"]!.AsArray(), h => h!.GetValue<string>() == "Zeile über Typname T3 (eindeutig)");
        Assert.Equal(3, abf["bestanden"]!.GetValue<int>());

        // mehrdeutig: T1–T3 stehen in beiden Tabellen (wie Typenübersicht und Masstabelle): über den Namen kein Soll
        var q = ProjektB4();
        var pdf2 = Ablegen(q, PdfZweiTabellen(o, "namen_doppelt.pdf", "Typ", ["T1", "T2", "T3"], ["T1", "T2", "T3"]));
        var e = Ok(q.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = OhneTypquellen(pdf2) }));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Mass L: Tabellenzeile für Typ T1, T2, T3 nicht eindeutig"));
        var abf2 = Abfrage(q, "familie_masse", new JsonObject { ["familie"] = "FAM-1" });
        var z2 = Zeile(abf2, "T1", "L");
        Assert.Null(z2["soll"]);
        Assert.True(z2["mehrdeutig"]!.GetValue<bool>());
        Assert.Contains(z2["hinweise"]!.AsArray(), h => h!.GetValue<string>().StartsWith("Mass L: Tabellenzeile für Typ T1 nicht eindeutig (Typname steht in 2 Zellen)"));
        Assert.Equal(3, abf2["ohneVergleich"]!.GetValue<int>());

        // nennt der Typ eine eigene Zelle, hat diese Vorrang vor dem Namen
        var d = OhneTypquellen(pdf2);
        var t1 = pdf2.Befunde.Where(x => x.Art == "tabelle" && x.Seite == 1).OrderByDescending(x => x.Box![3]).First();
        ((JsonObject)((JsonArray)d["typen"]!)[0]!)["quellen"] = new JsonObject { ["L"] = new JsonArray(UnterlagenHilfe.Zelle(pdf2, t1.Id, 1, 2).Id) };
        Ok(q.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d, ["familie"] = "FAM-1" }));
        var z3 = Zeile(Abfrage(q, "familie_masse", new JsonObject { ["familie"] = "FAM-1" }), "T1", "L");
        Assert.Equal(461, z3["soll"]!.GetValue<double>());
        Assert.DoesNotContain(z3["hinweise"]?.AsArray() ?? [], h => h!.GetValue<string>().Contains("Typname"));
    }
}
