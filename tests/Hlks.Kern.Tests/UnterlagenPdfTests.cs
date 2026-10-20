using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>PDF-Leser (Modul 2, B2) an einer erzeugten Datei mit bekannten Werten: Wörter, Zeilen, Tabelle mit Zellen und zusammengefassten
/// Zellen, Vektorpfade (Linie, Kreis, Bogen), Masslinie, stabile Ids.</summary>
public class UnterlagenPdfTests
{
    static UnterlagenBefunde Lesen1(out string pfad, out Ordner o)
    {
        o = new Ordner();
        pfad = TestPdf(o);
        return new PdfLeser().Lesen(pfad, "datenblatt.pdf", "0123456789abcdef", "pdf");
    }

    [Fact]
    public void Seiten_Groesse_in_pt_und_Reihenfolge()
    {
        var b = Lesen1(out _, out var o);
        using (o)
        {
            Assert.Equal(2, b.Seiten);
            var seiten = Art(b, "seite").ToList();
            Assert.Equal(2, seiten.Count);
            Assert.Equal(595, Zahl(((JsonObject)seiten[0].Wert!)["breite"]), 0.5);
            Assert.Equal(842, Zahl(((JsonObject)seiten[0].Wert!)["hoehe"]), 0.5);
            Assert.Equal("pt", seiten[0].Einheit);
            Assert.Equal("pdf 2", b.Leser);   // N4: Leserversion 2
        }
    }

    [Fact]
    public void Woerter_mit_Box_y_nach_oben()
    {
        var b = Lesen1(out _, out var o);
        using (o)
        {
            var w = Art(b, "wort").Single(x => x.Text() == "Testventil");
            Assert.Equal(1, w.Seite);
            // gezeichnet bei y = 60 von oben (Textoberkante) → in pt mit y nach oben etwa 842 − 60 − 14 … 842 − 60
            Assert.InRange(w.Box![1], 842 - 80, 842 - 55);
            Assert.InRange(w.Box[0], 100, 200);
            Assert.True(w.Box[2] > w.Box[0] && w.Box[3] > w.Box[1]);
            var z = Art(b, "zeile").First(x => x.Text().Contains("Datenblatt"));
            Assert.Equal("Datenblatt Testventil", z.Text());
        }
    }

    [Fact]
    public void Ids_sind_laufend_und_stabil()
    {
        var b = Lesen1(out var pfad, out var o);
        using (o)
        {
            Assert.All(b.Befunde.Select((x, i) => (x, i)), t => Assert.Equal($"0123456789ab#{t.i + 1}", t.x.Id));
            var b2 = new PdfLeser().Lesen(pfad, "datenblatt.pdf", "0123456789abcdef", "pdf");
            Assert.Equal(b.Befunde.Select(x => (x.Id, x.Art, x.Wert?.ToJsonString())), b2.Befunde.Select(x => (x.Id, x.Art, x.Wert?.ToJsonString())));
        }
    }

    [Fact]
    public void Tabelle_Seite1_mit_Kopf_Einheiten_und_Zellen()
    {
        var b = Lesen1(out _, out var o);
        using (o)
        {
            var t = Art(b, "tabelle").Single(x => x.Seite == 1);
            var w = (JsonObject)t.Wert!;
            Assert.Equal(4, w["spalten"]!.GetValue<int>());
            Assert.Equal(3, w["zeilen"]!.GetValue<int>());
            Assert.Equal(["Typ", "DN [mm]", "L [mm]", "H [mm]"], ((JsonArray)w["kopf"]!).Select(x => x!.GetValue<string>()).ToArray());
            var daten = ((JsonArray)w["daten"]!).Select(r => ((JsonArray)r!).Select(x => x!.GetValue<string>()).ToArray()).ToArray();
            Assert.Equal(["V-15", "15", "67", "35"], daten[0]);
            Assert.Equal(["V-25", "25", "87", "46"], daten[2]);

            var l = Zelle(b, t.Id, 1, 3);
            Assert.Equal(67, Zahl(l.Wert));
            Assert.Equal("mm", l.Einheit);
            Assert.Equal("L [mm]", l.Zusatz!["kopf"]!.GetValue<string>());
            Assert.Equal(1, l.Seite);
            Assert.Equal(1.0, l.Sicherheit);
            Assert.Equal("V-15", Zelle(b, t.Id, 1, 1).Text());
            // die Matrix «zellen» nennt die Nummer der Zelle (Id = Prüfsumme#Nummer)
            int nummer = ((JsonArray)((JsonArray)w["zellen"]!)[0]!)[2]!.GetValue<int>();
            Assert.Equal(l.Id, $"0123456789ab#{nummer}");
        }
    }

    [Fact]
    public void Tabelle_Seite2_zusammengefasste_Zellen_werden_wie_sie_stehen_gemeldet()
    {
        var b = Lesen1(out _, out var o);
        using (o)
        {
            var t = Art(b, "tabelle").Single(x => x.Seite == 2);
            var w = (JsonObject)t.Wert!;
            Assert.Equal(6, w["zeilen"]!.GetValue<int>());
            var daten = ((JsonArray)w["daten"]!).Select(r => ((JsonArray)r!).Select(x => x!.GetValue<string>()).ToArray()).ToArray();
            // H steht nur in der obersten Zeile der Zusammenfassung, die übrigen Zellen sind leer
            Assert.Equal(["200", "100", "130"], daten[0]);
            Assert.Equal(["300", "", "190"], daten[1]);
            Assert.Equal(["400", "", "255"], daten[2]);
            Assert.Equal(["200", "150", "193"], daten[3]);
            var zus = (JsonArray)t.Zusatz!["zusammengefasst"]!;
            Assert.Equal(2, zus.Count);
            Assert.All(zus, z => { Assert.Equal(2, z!["spalte"]!.GetValue<int>()); Assert.Equal(3, z["zeilen"]!.GetValue<int>()); });
            Assert.Equal([1, 4], zus.Select(z => z!["zeile"]!.GetValue<int>()).ToArray());
            var h100 = Zelle(b, t.Id, 1, 2);
            Assert.Equal(100, Zahl(h100.Wert));
            Assert.True(h100.Zusatz!["zusammengefasst"]!.GetValue<bool>());
            Assert.Equal(3, h100.Zusatz["zeilen"]!.GetValue<int>());
            Assert.Contains(b.Hinweise, h => h.Contains("mehrere Zeilen") && h.Contains("Spalte 2"));
        }
    }

    [Fact]
    public void Vektorpfade_Linie_Kreis_Bogen_Rechteck()
    {
        var b = Lesen1(out _, out var o);
        using (o)
        {
            // Masslinie bei y = 500 von oben → 842 − 500 = 342, von x 100 bis 300
            Assert.Contains(Art(b, "linie").Where(x => x.Seite == 1), l => l.Lage!.Count == 2
                && Math.Abs(l.Lage[0][1] - 342) < 0.5 && Math.Abs(l.Lage[1][1] - 342) < 0.5 && Math.Abs(Math.Abs(l.Lage[1][0] - l.Lage[0][0]) - 200) < 0.5);
            var k = Art(b, "kreis").Single(x => x.Seite == 1);
            Assert.Equal(20, Zahl(((JsonObject)k.Wert!)["radius"]), 0.2);
            var m = ((JsonObject)k.Wert!)["mitte"]!.AsArray();
            Assert.Equal(400, Zahl(m[0]), 0.3); Assert.Equal(842 - 650, Zahl(m[1]), 0.3);
            var bo = Art(b, "bogen").Single(x => x.Seite == 1);
            Assert.Equal(30, Zahl(((JsonObject)bo.Wert!)["radius"]), 0.3);
            // Rechteck 200 × 80 als geschlossene Polylinie
            var r = Art(b, "polylinie").Single(x => x.Seite == 1 && x.Zusatz?["rechteck"]?.GetValue<bool>() == true && Math.Abs(x.Box![2] - x.Box[0] - 200) < 0.5);
            Assert.Equal(80, r.Box![3] - r.Box[1], 0.5);
        }
    }

    [Fact]
    public void Masslinie_mit_Buchstabe_ist_ein_Kandidat_mit_Sicherheit_unter_eins()
    {
        var b = Lesen1(out _, out var o);
        using (o)
        {
            var m = Art(b, "mass").Single(x => x.Seite == 1);
            Assert.Equal("L", m.Text());
            Assert.InRange(m.Sicherheit, 0.3, 0.99);
            Assert.Equal(2, m.Lage!.Count);
            Assert.Equal(200, Math.Abs(m.Lage[1][0] - m.Lage[0][0]), 0.5);
            Assert.Equal("waagrecht", m.Zusatz!["richtung"]!.GetValue<string>());
            Assert.Equal(2, m.Zusatz["pfeile"]!.GetValue<int>());
            Assert.Equal(200, m.Zusatz["linienlaenge"]!.GetValue<double>(), 0.5);
        }
    }

    [Fact]
    public void Nicht_lesbare_Datei_gibt_Fehler_mit_Vorschlag()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "kaputt.pdf", "das ist kein PDF");
        var ex = Assert.Throws<HlksFehler>(() => new PdfLeser().Lesen(pfad, "kaputt.pdf", "0123456789ab", "pdf"));
        Assert.False(string.IsNullOrWhiteSpace(ex.Vorschlag));
    }
}

static class BefundErweiterungen
{
    /// <summary>Wert als Text (für Wörter, Zeilen und Zellen).</summary>
    public static string Text(this UnterlagenBefund b) => b.Wert is JsonValue v ? v.ToString() : b.Wert?.ToJsonString() ?? "";
}
