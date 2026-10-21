using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.RaumgrenzenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Nachbearbeitung N9: Ausrichtung der Grundrisse nach Geometrie (Planwinkel des Gebäudes, Vorgabe) oder nach Himmelsrichtung
/// (Projektinformation.nordwinkel). Testgebäude frei gewählt (keine Normwerte): Rechteck 10 × 6 m um 37° gedreht.</summary>
public class AnsichtAusrichtungTests
{
    const double Winkel = 37;

    static double[] D(double x, double y, double grad = Winkel) => Ansichtsausrichtung.Drehen([x, y], grad);

    /// <summary>Gebäude GEB, Geschoss EG, Grundriss AN-EG; Rechteck 10 × 6 m (Achsen) um 37° gedreht, Wände 200 mm.</summary>
    static Projekt Gedreht(bool mitRaum = true, double? nordwinkel = null)
    {
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-EG", ["geschoss"] = "EG",
            ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50, ["schnitthoehe"] = 1000 },
        }));
        if (nordwinkel is double n)
            Ok(p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Projektinformation", ["kennzeichen"] = "PI", ["eigenschaften"] = new JsonObject { ["nordwinkel"] = n } }));
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts(D(0, 0), D(10000, 0), D(10000, 6000), D(0, 6000)), ["dicke"] = 200, ["geschlossen"] = true,
        }));
        if (mitRaum)
        {
            var c = D(5000, 3000);
            Ok(p.Ausfuehren("raum_erkennen", new JsonObject { ["geschoss"] = "EG", ["punkt"] = new JsonArray(c[0], c[1]), ["nummer"] = "E.01" }));
        }
        return p;
    }

    static Wert PW(Projekt p, string geb = "GEB") => p.Modell.Finde(geb)!.Eigenschaft("planwinkel") ?? throw new Xunit.Sdk.XunitException("planwinkel fehlt");

    // ------------------------------------------------------------------ Planwinkel bestimmen

    [Theory]
    [InlineData(37, -37)]
    [InlineData(127, -37)]
    [InlineData(-37, 37)]
    [InlineData(0, 0)]
    [InlineData(90, 0)]
    [InlineData(180, 0)]
    [InlineData(89, 1)]
    [InlineData(45, 45)]
    [InlineData(-45, 45)]
    [InlineData(10.5, -10.5)]
    public void AusRichtung_KanteAchsparallel_DrehungZwischenMinus45Und45(double richtung, double erwartet)
    {
        var w = Planwinkel.AusRichtung(richtung);
        Assert.Equal(erwartet, w, 6);
        Assert.InRange(w, -45 + 1e-9, 45);
        // die Kante liegt danach achsparallel
        double rest = ((richtung + w) % 90 + 90) % 90;
        Assert.True(rest < 1e-6 || rest > 90 - 1e-6, $"{richtung}° + {w}° = {richtung + w}°");
    }

    [Fact]
    public void Planwinkel_RechteckUm37Grad_Minus37_GesetztMitDerErstenWand()
    {
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        var e = Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts(D(0, 0), D(10000, 0), D(10000, 6000), D(0, 6000)), ["dicke"] = 200, ["geschlossen"] = true,
        }));
        // im selben Paket wie die Wände (ein Rückgängig nimmt beides zurück), mit Hinweis
        Assert.Contains(e.Aenderungen, a => a.Kennzeichen == "GEB" && a.Klasse == "Gebaeude");
        Assert.Contains(e.Hinweise, h => h.Contains("Planwinkel") && h.Contains("GEB"));
        var w = PW(p);
        Assert.Equal(-37, w.AlsZahl()!.Value, 6);
        Assert.Equal("°", w.Einheit);
        Assert.Equal("berechnung", w.Herkunft.Art);
        Assert.Contains("längste Gebäudekante W-1", w.Herkunft.Quelle);   // W-1 und W-3 sind gleich lang: das kleinere Kennzeichen
        Assert.Contains("Länge 10.00 m", w.Herkunft.Quelle);
        Assert.Contains("Richtung 37.0°", w.Herkunft.Quelle);
        Assert.Contains("keine Aussenwand erkannt", w.Herkunft.Quelle);   // noch ohne Raum: Rückfall längste Wand

        Ok(p.Rueckgaengig());
        Assert.Null(p.Modell.Finde("GEB")!.Eigenschaft("planwinkel"));
    }

    [Fact]
    public void Planwinkel_MitRaum_Aussenwand_UndBefehlMitTrockenlauf()
    {
        var p = Gedreht();
        // planwinkel_bestimmen erkennt jetzt die Aussenwände (Raumgrenzen gegen aussen); der Wert bleibt −37°, die Quelle wird genauer
        var t = Ok(p.Ausfuehren("planwinkel_bestimmen", new JsonObject(), trockenlauf: true));
        Assert.True(t.Trockenlauf);
        Assert.Contains("keine Aussenwand", PW(p).Herkunft.Quelle);   // Trockenlauf ändert nichts
        var e = Ok(p.Ausfuehren("planwinkel_bestimmen", new JsonObject { ["gebaeude"] = "GEB" }));
        Assert.Contains(e.Hinweise, h => h.Contains("neu bestimmt"));
        var w = PW(p);
        Assert.Equal(-37, w.AlsZahl()!.Value, 6);
        Assert.Contains("(Aussenwand)", w.Herkunft.Quelle);
        // zweimal: unverändert, kein Paket
        var z = Ok(p.Ausfuehren("planwinkel_bestimmen", new JsonObject()));
        Assert.Equal("Keine Änderung.", z.Meldung);
        Assert.Contains(z.Hinweise, h => h.Contains("unverändert"));
    }

    [Fact]
    public void Planwinkel_AussenwandGehtVorLaengererInnenwand_RueckfallOhneAussenwand()
    {
        // Rechteck 12 × 8 m um 10° gedreht, dazu eine 13 m lange Wand schräg durch (40°): ohne Aussenwände ist sie die längste
        var p = Leer();
        Geschoss(p, "EG", 0, 3000);
        Ok(p.Ausfuehren("wand_zeichnen", new JsonObject
        {
            ["geschoss"] = "EG", ["punkte"] = Pts(D(0, 0, 10), D(12000, 0, 10), D(12000, 8000, 10), D(0, 8000, 10)), ["dicke"] = 200, ["geschlossen"] = true,
        }));
        Assert.Equal(-10, PW(p).AlsZahl()!.Value, 6);
        var a = D(-300, 0, 40);
        var lang = Wand(p, "EG", [a[0] + 1000, a[1] + 1000], [a[0] + 1000 + 13000 * Math.Cos(40 * Math.PI / 180), a[1] + 1000 + 13000 * Math.Sin(40 * Math.PI / 180)], 150);
        Assert.Equal(-10, PW(p).AlsZahl()!.Value, 6);   // bleibt: die Ansichten springen nicht, wenn eine Wand dazukommt

        var r = Planwinkel.Bestimmen(p.Modell, p.Katalog, p.Modell.Finde("GEB"));
        Assert.Equal(lang, r.Wand!.Kennzeichen);   // ohne Räume und Vorgaben: Rückfall längste Wand
        Assert.False(r.Aussen);
        Assert.Equal(-40, r.Winkel, 6);

        // Vorgabe angrenzung = aussen an den Rechteckwänden: die längste Aussenwand gilt
        foreach (var w in new[] { "W-1", "W-2", "W-3", "W-4" }) Eigenschaften(p, w, new JsonObject { ["angrenzung"] = "aussen" });
        Ok(p.Ausfuehren("planwinkel_bestimmen", new JsonObject()));
        Assert.Equal(-10, PW(p).AlsZahl()!.Value, 6);
        Assert.Contains("W-1 (Aussenwand)", PW(p).Herkunft.Quelle);
    }

    [Fact]
    public void Planwinkel_OhneWand_0_UndOhneGebaeudeAbgelehnt()
    {
        var p = Leer();
        var e = Ok(p.Ausfuehren("planwinkel_bestimmen", new JsonObject()));
        Assert.Equal(0, PW(p).AlsZahl()!.Value);
        Assert.Contains("keine Wand", PW(p).Herkunft.Quelle);
        Assert.Contains(e.Hinweise, h => h.Contains("Planwinkel 0"));

        var ohne = Projekt.Anlegen(Kat, null, "ohne Gebäude", "T", "pc");
        var n = ohne.Ausfuehren("planwinkel_bestimmen", new JsonObject());
        Assert.False(n.Angenommen);
        Assert.Contains("kein Gebäude", n.Fehler);
        Assert.False(string.IsNullOrWhiteSpace(n.Vorschlag));
    }

    [Fact]
    public void Planwinkel_VonHand_GiltUndBleibt()
    {
        var p = Gedreht();
        Eigenschaften(p, "GEB", new JsonObject { ["planwinkel"] = 12.5 });
        Wand(p, "EG", [20000, 0], [40000, 3000], 200);   // neue, längste Wand: der Winkel von Hand bleibt
        Assert.Equal(12.5, PW(p).AlsZahl()!.Value, 6);
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal(12.5, g.Drehung, 6);
        Assert.Equal("geometrie", g.Ausrichtung);
    }

    [Fact]
    public void Abfrage_Planwinkel_GespeichertBestimmtUndAnsicht()
    {
        var p = Gedreht(nordwinkel: 30);
        var o = (JsonObject)Tuer.Abfrage(p, "planwinkel", new JsonObject { ["ansicht"] = "AN-EG" });
        var g = o["gebaeude"]![0]!;
        Assert.Equal("GEB", g["gebaeude"]!.GetValue<string>());
        Assert.Equal(-37, g["planwinkel"]!.GetValue<double>(), 4);
        Assert.Equal("berechnung", g["herkunft"]!["art"]!.GetValue<string>());
        Assert.True(g["bestimmt"]!["aussenwand"]!.GetValue<bool>());
        Assert.Equal(10, g["bestimmt"]!["laenge_m"]!.GetValue<double>(), 3);
        var a = o["ansicht"]!;
        Assert.Equal("geometrie", a["ausrichtung"]!.GetValue<string>());
        Assert.Equal(-37, a["drehung"]!.GetValue<double>(), 4);
        Assert.Equal(67, a["nord"]!.GetValue<double>(), 4);   // Nord auf dem Blatt: nordwinkel − Drehung = 30 − (−37)
        Assert.True(a["gespeichert"]!.GetValue<bool>());
    }

    // ------------------------------------------------------------------ Ausrichtung der Ansicht

    [Fact]
    public void Himmelsrichtung_DrehtUmNordwinkel_NordOben_OhneNordwinkelHinweis()
    {
        var p = Gedreht(nordwinkel: 30);
        Eigenschaften(p, "AN-EG", new JsonObject { ["ausrichtung"] = "himmelsrichtung" });
        var g = Grafik.Plan(p, "AN-EG");
        Assert.Equal("himmelsrichtung", g.Ausrichtung);
        Assert.Equal(30, g.Drehung, 6);
        Assert.Equal(0, g.Nord, 6);

        var ohne = Gedreht();
        Eigenschaften(ohne, "AN-EG", new JsonObject { ["ausrichtung"] = "himmelsrichtung" });
        var a = Ansichtsausrichtung.Fuer(ohne, ohne.Modell.Finde("AN-EG")!);
        Assert.Equal(0, a.Drehung);
        Assert.Contains("Kein Nordwinkel", a.Hinweis);
        Assert.Equal(0, Grafik.Plan(ohne, "AN-EG").Drehung);
    }

    [Fact]
    public void Geometrie_IstVorgabe_UndNurGrundrisseWerdenGedreht()
    {
        var p = Gedreht(nordwinkel: 30);
        var g = Grafik.Plan(p, "AN-EG");   // ohne Eigenschaft ausrichtung: geometrie
        Assert.Equal("geometrie", g.Ausrichtung);
        Assert.Equal(-37, g.Drehung, 6);
        Assert.Equal(67, g.Nord, 6);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-S",
            ["eigenschaften"] = new JsonObject { ["art"] = "schnitt", ["massstab"] = 50, ["blick"] = "links", ["tiefe"] = 20000 },
            ["geometrie"] = new JsonObject { ["art"] = "linie", ["punkte"] = Pts([-2000, 3000], [12000, 3000]) },
        }));
        var s = Grafik.Plan(p, "AN-S");
        Assert.Equal(0, s.Drehung);
        Assert.Null(s.Ausrichtung);
    }

    [Fact]
    public void ProjektVorN9_OhneGespeichertenWinkel_WirdGerechnet_UndBeimSpeichernGleich()
    {
        var p = Gedreht();
        // Stand wie vor N9: planwinkel fehlt (von Hand entfernt); die Ansicht rechnet ihn, die nächste Wand speichert denselben Winkel
        Eigenschaften(p, "GEB", new JsonObject { ["planwinkel"] = null });
        Assert.Null(p.Modell.Finde("GEB")!.Eigenschaft("planwinkel"));
        var a = Ansichtsausrichtung.Fuer(p, p.Modell.Finde("AN-EG")!);
        Assert.Equal(-37, a.Drehung, 6);
        Assert.False(a.Gespeichert);
        Assert.Contains("noch nicht am Gebäude gespeichert", a.Quelle);
        // neue, längere Wand in anderer Richtung: gespeichert wird der Winkel vom Stand vor dem Befehl (die Ansicht springt nicht)
        Wand(p, "EG", [20000, 0], [40000, 3000], 200);
        Assert.Equal(-37, PW(p).AlsZahl()!.Value, 6);
        Assert.Equal(-37, Grafik.Plan(p, "AN-EG").Drehung, 6);
    }

    // ------------------------------------------------------------------ Grafik: Beschriftungen, Nordpfeil, Begrenzung

    [Fact]
    public void Grafik_RaumstempelWaagrecht_GestapeltAufDemBlatt()
    {
        var p = Gedreht();
        var g = Grafik.Plan(p, "AN-EG");
        var raum = g.Teile.Single(t => t.Ebene == "raum" && t.Primitive.Any(x => x.Art == "text"));
        var texte = raum.Primitive.Where(x => x.Art == "text").ToList();
        Assert.True(texte.Count >= 2);
        foreach (var t in texte)
        {
            Assert.Equal(37, t.Winkel ?? 0, 6);                  // im Modell +37°, mit der Drehung −37° auf dem Blatt waagrecht
            Assert.Null(t.Anker);                                  // angewandt und entfernt
        }
        // die Zeilen stehen auf dem Blatt senkrecht untereinander: in Ansichtsachsen gleiche x-Lage
        var a = Ansichtsausrichtung.Drehen(texte[0].Punkte[0], g.Drehung);
        var b = Ansichtsausrichtung.Drehen(texte[1].Punkte[0], g.Drehung);
        Assert.Equal(a[0], b[0], 3);
        Assert.True(a[1] > b[1]);
    }

    [Fact]
    public void Grafik_MasszahlFolgtDerWand_UndBleibtLesbar()
    {
        var p = Gedreht();
        // Bemassung entlang der Südwand (Richtung 37°) und eine entgegengesetzte (217°): beide auf dem Blatt waagrecht (0°)
        foreach (var (kz, von, bis) in new[] { ("BM-1", D(0, -1000), D(10000, -1000)), ("BM-2", D(10000, 7000), D(0, 7000)) })
            Ok(p.Ausfuehren("element_anlegen", new JsonObject
            {
                ["klasse"] = "Bemassung", ["kennzeichen"] = kz, ["bezuege"] = new JsonObject { ["ansicht"] = "AN-EG" },
                ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(von, bis) },
            }));
        var g = Grafik.Plan(p, "AN-EG");
        foreach (var kz in new[] { "BM-1", "BM-2" })
        {
            var t = g.Teile.Single(x => x.Id == p.Modell.Finde(kz)!.Id).Primitive.Single(x => x.Art == "text");
            double blatt = Ansichtsausrichtung.Norm((t.Winkel ?? 0) + g.Drehung);
            Assert.Equal(0, blatt, 6);
            Assert.Null(t.Mitdrehen);
        }
    }

    [Fact]
    public void Grafik_BemassungHorizontalGiltInDenAchsenDerAnsicht()
    {
        var p = Gedreht();
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Bemassung", ["kennzeichen"] = "BM-H", ["bezuege"] = new JsonObject { ["ansicht"] = "AN-EG" },
            ["eigenschaften"] = new JsonObject { ["richtung"] = "horizontal" },
            ["geometrie"] = new JsonObject { ["art"] = "polylinie", ["punkte"] = Pts(D(0, 0), D(10000, 6000)) },
        }));
        var g = Grafik.Plan(p, "AN-EG");
        var t = g.Teile.Single(x => x.Id == p.Modell.Finde("BM-H")!.Id).Primitive.Single(x => x.Art == "text");
        Assert.Equal("10.00", t.Text);   // horizontal auf dem Blatt = entlang der Gebäudekante: 10 m
    }

    [Fact]
    public void Grafik_NordpfeilZeigtNachGeografischNord_InBeidenAusrichtungen()
    {
        var p = Gedreht(nordwinkel: 30);
        Ok(p.Ausfuehren("element_anlegen", new JsonObject
        {
            ["klasse"] = "Symbol", ["kennzeichen"] = "SY-N", ["bezuege"] = new JsonObject { ["ansicht"] = "AN-EG" },
            ["eigenschaften"] = new JsonObject { ["art"] = "nordpfeil" },
            ["geometrie"] = new JsonObject { ["art"] = "punkt", ["punkte"] = Pts([20000, 0]) },
        }));
        foreach (var aus in new[] { "geometrie", "himmelsrichtung" })
        {
            Eigenschaften(p, "AN-EG", new JsonObject { ["ausrichtung"] = aus });
            var g = Grafik.Plan(p, "AN-EG");
            var umriss = g.Teile.Single(x => x.Id == p.Modell.Finde("SY-N")!.Id).Primitive.First(x => x.Art == "polygon" && x.Stil.Stift is not null);
            var spitze = umriss.Punkte[0];
            // Richtung der Spitze im Modell: geografisch Nord = 30° im Uhrzeigersinn ab +y, also 60° ab +x
            double modell = Math.Atan2(spitze[1] - 0, spitze[0] - 20000) * 180 / Math.PI;
            Assert.Equal(60, modell, 3);
            // auf dem Blatt: Richtung + Drehung = 90° − Nord (Nord im Uhrzeigersinn ab oben)
            Assert.Equal(90 - g.Nord, Ansichtsausrichtung.Norm(modell + g.Drehung), 3);
            if (aus == "himmelsrichtung") Assert.Equal(90, Ansichtsausrichtung.Norm(modell + g.Drehung), 3);   // Nord oben
        }
    }

    [Fact]
    public void Grafik_BegrenzungAnsicht_UmschliesstDasGedrehteGebaeudeEng()
    {
        var p = Gedreht();
        var g = Grafik.Plan(p, "AN-EG");
        var b = g.BegrenzungAnsicht!;
        // in Ansichtsachsen liegt das Rechteck achsparallel: 10 m + Wanddicke breit, 6 m + Wanddicke hoch (Stempel innen)
        Assert.Equal(10200, b[2] - b[0], 0);
        Assert.Equal(6200, b[3] - b[1], 0);
        var m = g.Begrenzung!;
        Assert.True(m[2] - m[0] > 11000);   // im Modell schräg: grösser
    }

    [Fact]
    public void Anwenden_TextOhneMitdrehen_UmAnker_TextMitMitdrehen_LesbarUndAufDerselbenSeite()
    {
        // Text mit Anker: dreht um den Anker gegen; mitdrehender Text bei 80° + 30° = 110° → umgeklappt auf −70°, links ↔ rechts, versetzt
        var anker = new Primitiv { Art = "text", Punkte = [[1000, 0]], Text = "A", Groesse = 2, Anker = [0, 0] };
        var mit = new Primitiv { Art = "text", Punkte = [[0, 0]], Text = "B", Groesse = 2, Winkel = 80, Ausrichtung = "links", Mitdrehen = true };
        var box = new Primitiv { Art = "polygon", Punkte = [[0, 0], [100, 0], [100, 50]], Anker = [0, 0] };
        var linie = new Primitiv { Art = "linie", Punkte = [[0, 0], [100, 0]] };
        Ansichtsausrichtung.Anwenden([new Grafikteil { Primitive = [anker, mit, box, linie] }], 30, 50);
        Assert.Equal(-30, anker.Winkel!.Value, 6);
        var a = Ansichtsausrichtung.Drehen([1000, 0], -30);
        Assert.Equal(a[0], anker.Punkte[0][0], 6); Assert.Equal(a[1], anker.Punkte[0][1], 6);
        Assert.Equal(-70 - 30, mit.Winkel!.Value, 6);       // auf dem Blatt −70°
        Assert.Equal("rechts", mit.Ausrichtung);
        var oben = Ansichtsausrichtung.Drehen([0, 1], 80);  // alte «oben»-Richtung
        Assert.Equal(oben[0] * 0.7 * 2 * 50, mit.Punkte[0][0], 6);
        Assert.Equal(oben[1] * 0.7 * 2 * 50, mit.Punkte[0][1], 6);
        Assert.Equal(-30, Math.Atan2(box.Punkte[1][1], box.Punkte[1][0]) * 180 / Math.PI, 6);
        Assert.Equal(100, linie.Punkte[1][0]);              // ohne Anker: dreht mit der Ansicht
    }

    // ------------------------------------------------------------------ PDF

    static string Inhalt(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    [Fact]
    public void Pdf_DrehtDieAnsichtAnEinerStelle_UndNordpfeilNachGeografischNord()
    {
        var p = Gedreht(nordwinkel: 30);
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "N9-1", ["titel"] = "Test" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG" }));
        var s = Inhalt(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        // eine Drehmatrix um −37° (cos 0.7986, sin 0.6018) für das Fenster
        Assert.Matches(@"0\.7986\d* -?0\.6018\d* -?0\.6018\d* 0\.7986\d* [-\d.]+ [-\d.]+ cm", s);

        Eigenschaften(p, "AN-EG", new JsonObject { ["ausrichtung"] = "himmelsrichtung" });
        var h = Inhalt(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        Assert.Matches(@"0\.866\d* -?0\.5 -?0\.5 0\.866\d* [-\d.]+ [-\d.]+ cm|0\.866\d* -?0\.5\d* -?0\.5\d* 0\.866\d* [-\d.]+ [-\d.]+ cm", h);   // 30°
    }

    [Fact]
    public void Pdf_OhneDrehungKeineDrehmatrix()
    {
        var p = ArchitekturHilfe.Rechteck();
        Ok(p.Ausfuehren("planblatt_anlegen", new JsonObject { ["nummer"] = "N9-0", ["titel"] = "Test" }));
        Ok(p.Ausfuehren("ansicht_platzieren", new JsonObject { ["planblatt"] = "PB-1", ["ansicht"] = "AN-EG" }));
        Assert.Equal(0, PW(p).AlsZahl()!.Value);
        Assert.Equal(0, Grafik.Plan(p, "AN-EG").Drehung);
        var s = Inhalt(Planausgabe.Pdf(p, "PB-1", unkomprimiert: true));
        Assert.DoesNotMatch(@"0\.7986", s);
    }
}
