using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using PdfSharp.Pdf.IO;

namespace Hlks.Kern.Tests;

/// <summary>Modul 1a, Teil B4: Planblätter, Ansichtsfenster, PDF-Ausgabe. Die Zeichnung wird mit selbst gebauten
/// Plangrafik-Objekten geprüft, damit sie nicht von den Grafikquellen der anderen Teile abhängt.</summary>
public class PlanTests
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    static Projekt Neu() => Beispielprojekt.Erzeugen(Kat, null);

    static BefehlsErgebnis Blatt(Projekt p, string nummer = "2317-101", string titel = "Grundrisse Sanitär", string? format = null, bool hoch = false)
    {
        var o = new JsonObject { ["nummer"] = nummer, ["titel"] = titel, ["hochformat"] = hoch };
        if (format is not null) o["format"] = format;
        return p.Ausfuehren("planblatt_anlegen", o);
    }

    static BefehlsErgebnis Platzieren(Projekt p, string blatt, string ansicht, double? x = null, double? y = null, bool trocken = false)
    {
        var o = new JsonObject { ["planblatt"] = blatt, ["ansicht"] = ansicht };
        if (x is not null && y is not null) o["lage"] = new JsonArray(x, y);
        return p.Ausfuehren("ansicht_platzieren", o, trocken);
    }

    static Primitiv Polygon(double[][] aussen, double[][]? loch = null, Stil? stil = null) => new()
    {
        Art = "polygon", Punkte = [.. aussen], Loecher = loch is null ? null : [[.. loch]],
        Stil = stil ?? new Stil { Stift = Stifte.Dick, Fuellung = "schnitt", Deckkraft = 0.25, Muster = "beton" },
    };

    /// <summary>Kleiner Grundriss 8 × 6 m in Modell-mm: Wandumriss mit Schraffur, Tür (Blatt und Bogen), Achse, Raumtext, Kreis;
    /// dazu ein Teil nur für den Bildschirm, der nicht im PDF stehen darf.</summary>
    static Plangrafik Grundriss(string ansicht = "AN-GR-EG", double massstab = 50)
    {
        var g = new Plangrafik { Ansicht = ansicht, Art = "grundriss", Massstab = massstab };
        g.Teile.Add(new Grafikteil
        {
            Ebene = "wand",
            Primitive =
            [
                Polygon([[0, 0], [8000, 0], [8000, 6000], [0, 6000]], [[200, 200], [200, 5800], [7800, 5800], [7800, 200]]),
                Polygon([[1000, 1000], [3000, 1000], [3000, 3000], [1000, 3000]], null, new Stil { Stift = Stifte.Duenn, Muster = "putz" }),
                Polygon([[4000, 1000], [6000, 1000], [6000, 3000], [4000, 3000]], null, new Stil { Stift = Stifte.Duenn, Muster = "backstein" }),
            ],
        });
        g.Teile.Add(new Grafikteil
        {
            Ebene = "oeffnung",
            Primitive =
            [
                new Primitiv { Art = "linie", Punkte = [[2000, 0], [2000, 900]], Stil = new Stil { Stift = Stifte.Mittel } },
                new Primitiv { Art = "bogen", Punkte = [[2000, 0]], Radius = 900, Von = 0, Bis = 90, Stil = new Stil { Stift = Stifte.Duenn } },
            ],
        });
        g.Teile.Add(new Grafikteil
        {
            Ebene = "achse",
            Primitive =
            [
                new Primitiv { Art = "linie", Punkte = [[-1000, 3000], [9000, 3000]], Stil = new Stil { Stift = Stifte.Duenn, Linie = "strichpunkt" } },
                new Primitiv { Art = "polylinie", Punkte = [[0, -500], [4000, -500], [8000, -500]], Stil = new Stil { Stift = Stifte.Duenn, Linie = "strich" } },
                new Primitiv { Art = "kreis", Punkte = [[-1400, 3000]], Radius = 200, Stil = new Stil { Stift = Stifte.Duenn } },
            ],
        });
        g.Teile.Add(new Grafikteil
        {
            Ebene = "beschriftung",
            Primitive =
            [
                new Primitiv { Art = "text", Punkte = [[4000, 4500]], Text = "SICHTBAR 1.01\nBüro 23.40 m2", Groesse = 2.5, Ausrichtung = "mitte", Fett = true, Stil = new Stil { Stift = null } },
                new Primitiv { Art = "text", Punkte = [[100, 100]], Text = "DREHUNG", Groesse = 2.0, Winkel = 90, Stil = new Stil { Stift = null, Farbe = "#cc0000" } },
            ],
        });
        g.Teile.Add(new Grafikteil
        {
            Ebene = "raum",
            Primitive =
            [
                new Primitiv { Art = "text", Punkte = [[500, 500]], Text = "NURBILDSCHIRM", Groesse = 2.0, Stil = new Stil { NurBildschirm = true, Stift = null } },
                new Primitiv { Art = "polygon", Punkte = [[300, 300], [900, 300], [900, 900]], Stil = new Stil { NurBildschirm = true, Fuellung = "#12ab34" } },
            ],
        });
        g.Begrenzung = Hlks.Kern.Grafik.Begrenzung(g.Teile);
        return g;
    }

    static Func<Projekt, string, Plangrafik> Fake() => (_, a) => Grundriss(a, a == "AN-GR-1.OG" ? 100 : 50);

    static string Inhalt(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    /// <summary>Titelband «Planinhalt» einschalten (Modul 7 Teil A: abschaltbar, Vorgabe aus): Eigenschaft titelband am Planblatt.</summary>
    static void Band(Projekt p, string blatt = "PB-1") =>
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = blatt, ["eigenschaften"] = new JsonObject { ["titelband"] = true } }).Angenommen);

    static (double Breite, double Hoehe) Seite(byte[] pdf)
    {
        using var ms = new MemoryStream(pdf);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
        var s = doc.Pages[0];
        return (s.Width.Point / 72 * 25.4, s.Height.Point / 72 * 25.4);
    }

    // ------------------------------------------------------------------ Befehle

    [Fact]
    public void Planblatt_AnlegenMitVorgabenUndKennzeichen()
    {
        var p = Neu();
        var e = Blatt(p);
        Assert.True(e.Angenommen, e.Meldung);
        var b = p.Modell.Hole("PB-1");
        Assert.Equal("Planblatt", b.Klasse);
        Assert.Equal("2317-101", b.Text("nummer"));
        Assert.Equal("Grundrisse Sanitär", b.Text("titel"));
        Assert.Equal("A3", b.Text("format"));                       // Vorgabe
        Assert.False(Grafik.Bool(b, "hochformat"));
        Assert.Contains(e.Hinweise, h => h.Contains("PB-1") && h.Contains("420"));
        Assert.True(Blatt(p, "2317-102", "Schnitte", "a2", true).Angenommen);
        Assert.Equal("A2", p.Modell.Hole("PB-2").Text("format"));    // Schreibweise vereinheitlicht
        Assert.True(Grafik.Bool(p.Modell.Hole("PB-2"), "hochformat"));
    }

    [Fact]
    public void Planblatt_DoppelteNummerUndUnbekanntesFormatWerdenAbgelehnt()
    {
        var p = Neu();
        Assert.True(Blatt(p).Angenommen);
        var doppelt = Blatt(p, titel: "anderer Titel");
        Assert.False(doppelt.Angenommen);
        Assert.Contains("PB-1", doppelt.Fehler);
        Assert.False(string.IsNullOrWhiteSpace(doppelt.Vorschlag));

        var format = Blatt(p, "2317-200", "x", "A5");
        Assert.False(format.Angenommen);
        Assert.Contains("A4", format.Vorschlag);
        Assert.Single(p.Modell.DerKlasse("Planblatt"));
    }

    [Fact]
    public void Ansicht_PlatzierenLegtAnsichtsfensterAn()
    {
        var p = Neu();
        Blatt(p);
        var e = Platzieren(p, "PB-1", "AN-GR-EG", 210, 160);
        Assert.True(e.Angenommen, e.Meldung);
        var f = p.Modell.DerKlasse("Ansichtsfenster").Single();
        Assert.Equal(p.Modell.Hole("PB-1").Id, f.TeilVon);
        Assert.Equal(p.Modell.Hole("AN-GR-EG").Id, f.Bezuege["ansicht"]);
        Assert.Equal("punkt", f.Geometrie!.Art);
        Assert.Equal(210, f.Geometrie.Punkte[0][0]);
        Assert.Equal(160, f.Geometrie.Punkte[0][1]);
        Assert.StartsWith("AF-", f.Kennzeichen);
    }

    [Fact]
    public void Ansicht_NurAufEinemBlattAberDoppeltWirdAbgelehntMitVorschlag()
    {
        var p = Neu();
        Blatt(p); Blatt(p, "2317-102", "Zweites Blatt");
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 100).Angenommen);
        foreach (var blatt in new[] { "PB-1", "PB-2" })
        {
            var zweites = Platzieren(p, blatt, "AN-GR-EG", 200, 100);
            Assert.False(zweites.Angenommen);
            Assert.Contains("AN-GR-EG", zweites.Fehler);
            Assert.Contains("PB-1", zweites.Fehler);
            Assert.Contains("element_aendern", zweites.Vorschlag);
        }
        Assert.Single(p.Modell.DerKlasse("Ansichtsfenster"));
        Assert.True(Platzieren(p, "PB-2", "AN-GR-1.OG", 100, 100).Angenommen);     // andere Ansicht geht
    }

    [Fact]
    public void Ansicht_UngueltigeEingabenWerdenAbgelehnt()
    {
        var p = Neu();
        Blatt(p);
        var schema = Platzieren(p, "PB-1", "AN-3D", 100, 100);                 // 3D hat keine Plangrafik
        Assert.False(schema.Angenommen);
        Assert.Contains("grundriss", schema.Vorschlag);
        Assert.False(Platzieren(p, "PB-1", "AN-GR-EG", 5000, 100).Angenommen); // ausserhalb A3 quer
        Assert.False(Platzieren(p, "PB-9", "AN-GR-EG", 100, 100).Angenommen);  // Blatt gibt es nicht
        Assert.False(Platzieren(p, "PB-1", "WT-E.02", 100, 100).Angenommen);   // keine Ansicht
        Assert.False(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-GR-EG", ["lage"] = new JsonArray(1) }).Angenommen);
        Assert.Empty(p.Modell.DerKlasse("Ansichtsfenster"));
    }

    [Fact]
    public void Ansicht_TrockenlaufAendertNichtsUndRueckgaengigStelltHer()
    {
        var p = Neu();
        Blatt(p);
        var start = p.Modell.Pruefsumme();
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 100, trocken: true).Angenommen);
        Assert.Equal(start, p.Modell.Pruefsumme());
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 100).Angenommen);
        Assert.True(p.Rueckgaengig().Angenommen);
        Assert.Empty(p.Modell.DerKlasse("Ansichtsfenster"));
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 100).Angenommen);        // nach dem Rückgängig wieder frei
    }

    [Fact]
    public void Ansicht_OhneLageKommtInFreienBereichUndUeberdecktNichts()
    {
        var p = Neu();
        Blatt(p, format: "A1");
        var erste = Platzieren(p, "PB-1", "AN-GR-EG");
        Assert.True(erste.Angenommen, erste.Meldung);
        var zweite = Platzieren(p, "PB-1", "AN-GR-1.OG");
        Assert.True(zweite.Angenommen, zweite.Meldung);
        var l1 = p.Modell.Hole("AF-1").Geometrie!.Punkte[0];
        var l2 = p.Modell.Hole("AF-2").Geometrie!.Punkte[0];
        Assert.NotEqual((l1[0], l1[1]), (l2[0], l2[1]));
        foreach (var l in new[] { l1, l2 })
        {
            Assert.InRange(l[0], 0, 841);                 // A1 quer: 841 × 594 mm
            Assert.InRange(l[1], 0, 594);
        }
        // Erste Ansicht: Mitte des freien Bereichs über dem Plankopf (Blattmitte in x)
        Assert.Equal(841 / 2.0, l1[0], 0.5);
    }

    [Fact]
    public void Faehigkeiten_NennenDieNeuenBefehleUndDiePdfAbfrage()
    {
        var p = Neu();
        var f = (JsonObject)Tuer.Abfrage(p, "faehigkeiten");
        var namen = ((JsonArray)f["befehle"]!).Select(b => b!["name"]!.GetValue<string>()).ToList();
        Assert.Contains("planblatt_anlegen", namen);
        Assert.Contains("ansicht_platzieren", namen);
        Assert.Contains(((JsonArray)f["abfragen"]!), a => a!["name"]!.GetValue<string>() == "plan_pdf");
    }

    // ------------------------------------------------------------------ Blattformate

    [Theory]
    [InlineData("A4", false, 297, 210)]
    [InlineData("A4", true, 210, 297)]
    [InlineData("A3", false, 420, 297)]
    [InlineData("a2", false, 594, 420)]
    [InlineData("A1", false, 841, 594)]
    [InlineData("A0", false, 1189, 841)]
    [InlineData("A0", true, 841, 1189)]
    public void Blattformate_SindIso216(string format, bool hoch, double b, double h)
    {
        var g = Blattformate.Groesse(format, hoch);
        Assert.Equal(b, g.Breite);
        Assert.Equal(h, g.Hoehe);
    }

    // ------------------------------------------------------------------ PDF

    [Fact]
    public void Pdf_BeginntMitPdfUndHatDieGroesseA3Quer()
    {
        var p = Neu();
        Blatt(p);
        var pdf = Planausgabe.Pdf(p, "PB-1");
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
        var (b, h) = Seite(pdf);
        Assert.Equal(420, b, 0.1);
        Assert.Equal(297, h, 0.1);
        using var ms = new MemoryStream(pdf);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1190.55, doc.Pages[0].Width.Point, 0.1);        // A3 quer in pt
        Assert.Equal(841.89, doc.Pages[0].Height.Point, 0.1);
    }

    [Theory]
    [InlineData("A4", true, 210, 297)]
    [InlineData("A4", false, 297, 210)]
    [InlineData("A1", false, 841, 594)]
    [InlineData("A0", true, 841, 1189)]
    public void Pdf_SeitengroesseFolgtFormatUndRichtung(string format, bool hoch, double b, double h)
    {
        var p = Neu();
        Blatt(p, format: format, hoch: hoch);
        var (pb, ph) = Seite(Planausgabe.Pdf(p, "PB-1"));
        Assert.Equal(b, pb, 0.1);
        Assert.Equal(h, ph, 0.1);
    }

    [Fact]
    public void Pdf_ZeigtPlankopfMitProjektinformationAenderungenUndMassstab()
    {
        var p = Neu();
        Assert.True(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation",
            ["eigenschaften"] = new JsonObject { ["phase"] = "Bauprojekt", ["zeile1"] = "MFH Musterweg", ["zeile2"] = "Musterweg 12, 3000 Bern", ["projektnummer"] = "2317" },
        }).Angenommen);
        Assert.True(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "2317-101", ["titel"] = "Grundriss Erdgeschoss", ["format"] = "A3" }).Angenommen);
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "PB-1",
            ["eigenschaften"] = new JsonObject
            {
                ["datum"] = "17.10.2026", ["kontrolliert"] = "CS 17.10.2026", ["titelband"] = true,
                ["aenderungen"] = new JsonArray(new JsonObject { ["index"] = "a", ["datum"] = "18.10.2026", ["text"] = "Fenster verschoben" }),
            },
        }).Angenommen);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 200, 160).Angenommen);

        var s = Inhalt(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true, Fake()));
        foreach (var erwartet in new[]
                 {
                     "2317-101", "Grundriss Erdgeschoss", "Bauprojekt", "MFH Musterweg", "17.10.2026", "CS 17.10.2026",
                     "Fenster verschoben", "1 : 50", "Masse in cm", "A3 quer", "H+K Planungs AG", "Plannummer", "Kontrolliert", "Planinhalt",
                 })
            Assert.Contains(erwartet, s);
        Assert.Contains("Grundriss EG Sanit", s);          // Name der Ansicht unter dem Fenster
        Assert.Contains(" m)", s);                          // grafischer Massstab: Beschriftung mit Einheit
    }

    [Fact]
    public void Pdf_VerschiedeneMassstaebeGebenDivUndOhneFensterStehtKeinMassstab()
    {
        var p = Neu();
        Blatt(p);
        var leer = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.Contains("1 : xx", leer);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 150).Angenommen);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-1.OG", 300, 150).Angenommen);
        var s = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.Contains("div.", s);
        Assert.Contains("1:100", s);
        Assert.Contains("1:50", s);
    }

    [Fact]
    public void Pdf_EinheitDerMasseKommtAusDenAnsichtenDesBlatts()
    {
        var p = Neu();
        Blatt(p);
        Band(p);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 150).Angenommen);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-1.OG", 300, 150).Angenommen);
        // beide Sanitär-Grundrisse: Vorgabe cm, überall gleich → ein Hinweis, nichts im Titel
        var gleich = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.Contains("Masse in cm", gleich);
        Assert.DoesNotContain("siehe Ansichtstitel", gleich);
        Assert.DoesNotContain("1:50, Masse", gleich);

        // ein Fenster auf mm gestellt → gemischt: Hinweis «siehe Ansichtstitel», Einheit in jedem Titel
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-GR-1.OG", ["eigenschaften"] = new JsonObject { ["bemassungEinheit"] = "mm" } }).Angenommen);
        var gemischt = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.Contains("Masse in cm, mm siehe Ansichtstitel", gemischt);
        Assert.Contains("1:50, Masse in cm", gemischt);
        Assert.Contains("1:100, Masse in mm", gemischt);
    }

    [Fact]
    public void Pdf_AussparungsplanArchitekturZeigtMasseInCmWieSeineKetten()
    {
        // Befund Hauptlauf 19.10.2026 (Abnahme 3b): Ansicht ohne HLKS-Gewerk = Architektur → «m»; im Aussparungsplan stehen
        // die Massketten aber in cm (AussparungGrafik.Einheit) – der Kopf muss dasselbe sagen
        var p = Neu();
        Blatt(p);
        Band(p);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 100, 150).Angenommen);
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-GR-EG", ["eigenschaften"] = new JsonObject { ["gewerke"] = new JsonArray("architektur") } }).Angenommen);
        Assert.Contains("Masse in m", Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake())));
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "AN-GR-EG", ["eigenschaften"] = new JsonObject { ["darstellung"] = "aussparungen" } }).Angenommen);
        var s = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.Contains("Masse in cm", s);
        Assert.DoesNotContain("Masse in m", s.Replace("Masse in mm", ""));
    }

    [Fact]
    public void Pdf_PrimitiveLandenImPdf_NurBildschirmNicht()
    {
        var p = Neu();
        Blatt(p);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 210, 150).Angenommen);
        var s = Inhalt(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true, Fake()));

        // Text, mehrzeilig und gedreht
        Assert.Contains("SICHTBAR 1.01", s);
        Assert.Contains("DREHUNG", s);
        Assert.Contains("BT", s);
        // Linien, Polygone, Kreis und Bogen als Pfadoperatoren
        Assert.Matches(@"\d m\r?\n", s);                       // moveto
        Assert.Matches(@"\d l\r?\n", s);                       // lineto
        Assert.Matches(@"\d c\r?\n", s);                       // Kurve (Kreis, Bogen)
        Assert.Contains("f*", s);                              // Füllung gerade-ungerade (Wandumriss mit Loch)
        Assert.Matches(@"W\*? n", s);                          // Zuschnitt (Fenster, Schraffur)
        Assert.Matches(@"\[[\d. ]+\]0 d", s);                          // Strichmuster der Linienarten
        // Schraffur beton (zwei Winkel) und putz (Punkte) und backstein erzeugen viele einzelne Striche
        Assert.True(Regex.Matches(s, @"\d m\r?\n").Count > 300, "Schraffuren fehlen");
        // Bildschirmhilfen fehlen
        Assert.DoesNotContain("NURBILDSCHIRM", s);
        Assert.DoesNotContain("0.071 0.671 0.204 rg", s);
    }

    [Fact]
    public void Pdf_NurBildschirmTeileAendernDieGroesseNicht()
    {
        var p = Neu();
        Blatt(p);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 210, 150).Angenommen);
        var ohne = Planausgabe.Pdf(p, "PB-1", true, (_, a) =>
        {
            var g = Grundriss(a);
            g.Teile.RemoveAll(t => t.Ebene == "raum");
            return g;
        });
        var mit = Planausgabe.Pdf(p, "PB-1", true, Fake());
        Assert.Equal(Inhalt(ohne).Length, Inhalt(mit).Length);   // nichts von «raum» (nur Bildschirm) ist geschrieben
    }

    [Fact]
    public void Pdf_KomprimiertIstKleinerAlsUnkomprimiert()
    {
        var p = Neu();
        Blatt(p);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 210, 150).Angenommen);
        var klein = Planausgabe.Pdf(p, "PB-1", false, Fake());
        var gross = Planausgabe.Pdf(p, "PB-1", true, Fake());
        Assert.True(klein.Length < gross.Length);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(klein, 0, 4));
        Seite(klein);
    }

    [Fact]
    public void Pdf_UnbekanntesBlattGibtFehlerMitVorschlag()
    {
        var p = Neu();
        var f = Assert.Throws<HlksFehler>(() => Planausgabe.Pdf(p, "PB-7"));
        Assert.Contains("planblatt_anlegen", f.Vorschlag);
        Assert.Throws<HlksFehler>(() => Planausgabe.Pdf(p, "AN-GR-EG"));            // kein Planblatt
        Blatt(p);
        Assert.NotEmpty(Planausgabe.Pdf(p, "2317-101"));                              // auch über die Plannummer
    }

    [Fact]
    public void Pdf_AusschnittDerAnsichtBestimmtDasFenster()
    {
        var p = Neu();
        Blatt(p);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 210, 150).Angenommen);
        var ohne = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.True(p.Ausfuehren("element_aendern", new JsonObject
        {
            ["element"] = "AN-GR-EG",
            ["eigenschaften"] = new JsonObject { ["ausschnitt"] = new JsonObject { ["minx"] = 0, ["miny"] = 0, ["maxx"] = 4000, ["maxy"] = 3000 } },
        }).Angenommen);
        var mit = Inhalt(Planausgabe.Pdf(p, "PB-1", true, Fake()));
        Assert.NotEqual(ohne, mit);
        // Fenster 4000/50 × 3000/50 = 80 × 60 mm um die Mitte (210, 150): x von 170 bis 250 mm = 481.8898 bis 708.6614 pt (Zuschnitt)
        Assert.Contains("481.8898", mit);
        Assert.Contains("708.6614", mit);
        Assert.DoesNotContain("481.8898", ohne);
    }

    [Fact]
    public void Pdf_BeispielDateiFuerDieSichtpruefung()
    {
        var p = Neu();
        p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Projektinformation",
            ["eigenschaften"] = new JsonObject { ["phase"] = "Bauprojekt", ["zeile1"] = "MFH Musterweg (fiktiv)", ["zeile2"] = "Musterweg 12, 3000 Bern", ["projektnummer"] = "0000" },
        });
        Blatt(p, "0000-101", "Grundrisse EG und 1.OG, Sanitär", "A2");
        Assert.True(Platzieren(p, "PB-1", "AN-GR-EG", 170, 250).Angenommen);
        Assert.True(Platzieren(p, "PB-1", "AN-GR-1.OG", 430, 250).Angenommen);
        var antwort = (JsonObject)Tuer.Abfrage(p, "plan_pdf", new JsonObject { ["planblatt"] = "PB-1", ["datei"] = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "B4-Beispiel.pdf") });
        Assert.True(File.Exists(antwort["datei"]!.GetValue<string>()));
    }

    // ------------------------------------------------------------------ Abfrage plan_pdf

    [Fact]
    public void Abfrage_PlanPdfSchreibtDateiUndMeldetDatenSeitenGroesse()
    {
        var p = Neu();
        Blatt(p);
        var ordner = Path.Combine(Path.GetTempPath(), "hlks_plan_" + Guid.NewGuid().ToString("N")[..8], "neu");
        try
        {
            var ziel = Path.Combine(ordner, "plan.pdf");
            var a = (JsonObject)Tuer.Abfrage(p, "plan_pdf", new JsonObject { ["planblatt"] = "PB-1", ["datei"] = ziel });
            Assert.Equal(ziel, a["datei"]!.GetValue<string>());
            Assert.Equal(1, a["seiten"]!.GetValue<int>());
            var bytes = File.ReadAllBytes(ziel);
            Assert.Equal(bytes.Length, a["groesseBytes"]!.GetValue<int>());
            Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(ordner)!, true); } catch (IOException) { } }
    }

    [Fact]
    public void Abfrage_PlanPdfOhneDateiSchreibtInDenTempOrdnerUnterDerPlannummer()
    {
        var p = Neu();
        Blatt(p, "T/4711:x", "Titel");
        var a = (JsonObject)Tuer.Abfrage(p, "plan_pdf", new JsonObject { ["planblatt"] = "PB-1" });
        var datei = a["datei"]!.GetValue<string>();
        Assert.Equal(Path.Combine(Path.GetTempPath(), "HLKS_CAD", "T_4711_x.pdf"), datei);   // ungültige Zeichen ersetzt
        Assert.True(File.Exists(datei));
        File.Delete(datei);
    }

    [Fact]
    public void Abfrage_PlanPdfMitUnschreibbarerDateiGibtFehler()
    {
        var p = Neu();
        Blatt(p);
        var f = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "plan_pdf", new JsonObject { ["planblatt"] = "PB-1", ["datei"] = "Q:\\gibt\\es\\nicht\\<>|.pdf" }));
        Assert.False(string.IsNullOrWhiteSpace(f.Vorschlag));
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "plan_pdf", new JsonObject()));          // planblatt fehlt
    }

    // ------------------------------------------------------------------ Abbildung

    [Fact]
    public void Abbildung_RechnetModellMmInPapierMmUm()
    {
        // Modellpunkt (4000, 3000) kommt auf den Papierpunkt (210, 150); Massstab 1:50
        var a = Abbildung.Fuer(50, 210, 150, 4000, 3000);
        Assert.Equal(210, a.X(4000), 1e-9);
        Assert.Equal(150, a.Y(3000), 1e-9);
        Assert.Equal(210 + 20, a.X(5000), 1e-9);               // 1000 mm Modell = 20 mm Papier
        Assert.Equal(150 - 40, a.Y(1000), 1e-9);
        Assert.Equal(3.5, Abbildung.Papier.X(3.5));
    }
}
