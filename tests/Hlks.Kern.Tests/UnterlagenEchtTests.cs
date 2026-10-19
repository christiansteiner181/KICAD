using System.Diagnostics;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Echte Hersteller-Datenblätter (nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten\familien; fehlen sie, kehren die Tests sofort
/// zurück – die Dateien liegen nie im Repo). Prüft Tabellen, zusammengefasste Zellen, Masslinien und die Zeiten. Preise der Schmidlin-
/// Preisbücher werden nicht ausgewertet, nur Laufzeit und Anzahl Tabellen.</summary>
[CollectionDefinition("UnterlagenZeit", DisableParallelization = true)]
public sealed class UnterlagenZeitSammlung;

[Collection("UnterlagenZeit")]
public class UnterlagenEchtTests
{
    static string[][] Daten(UnterlagenBefund t) =>
        ((JsonArray)((JsonObject)t.Wert!)["daten"]!).Select(r => ((JsonArray)r!).Select(x => x!.GetValue<string>()).ToArray()).ToArray();

    static string[] Kopf(UnterlagenBefund t) => ((JsonArray)((JsonObject)t.Wert!)["kopf"]!).Select(x => x!.GetValue<string>()).ToArray();

    static UnterlagenBefunde Lesen(string pfad) => new PdfLeser().Lesen(pfad, Path.GetFileName(pfad), "0123456789abcdef", "pdf");

    [Fact]
    public void Belimo_Typenuebersicht_Seite1_Tabelle_und_Masstabelle_Seite3_ist_ein_Rasterbild()
    {
        var pfad = Echt("familien/belimo_R2..-S.._datasheet_de-ch.pdf");
        if (pfad is null) return;
        var b = Lesen(pfad);
        Assert.Equal(4, b.Seiten);
        // Seite 1: Typenübersicht als Tabelle mit den Zellen der Zeile R2015-S1
        var t = Art(b, "tabelle").Single(x => x.Seite == 1 && Daten(x).Any(r => r[0] == "R2015-S1"));
        Assert.Equal(["Typ", "DN", "Rp [\"]", "Kvs [m³/h]", "PN"], Kopf(t));
        Assert.Equal(["R2015-S1", "15", "1/2", "15", "40"], Daten(t)[0]);
        Assert.Equal(["R2032-S3", "32", "1 1/4", "32", "25"], Daten(t)[3]);   // «1 1/4» bleibt eine Zelle
        Assert.Equal(6, Daten(t).Length);
        Assert.Equal(15, Zahl(Zelle(b, t.Id, 1, 4).Wert));
        Assert.Equal("m³/h", Zelle(b, t.Id, 1, 4).Einheit);
        // Seite 3: Massbild und Masstabelle sind eingebettete Rasterbilder – kein Text, also keine Tabelle: Befund bild + Hinweis
        Assert.DoesNotContain(Art(b, "tabelle"), x => x.Seite == 3);
        var bilder = Art(b, "bild").Where(x => x.Seite == 3 && x.Zusatz?["art"]?.GetValue<string>() == "eingebettet").ToList();
        Assert.Contains(bilder, x => ((JsonObject)x.Wert!)["pixelBreite"]!.GetValue<int>() == 2177 && ((JsonObject)x.Wert)["pixelHoehe"]!.GetValue<int>() == 420);
        Assert.Contains(b.Hinweise, h => h.StartsWith("Seite 3: Rasterbild 2177") && h.Contains("unterlage_bild"));
        Assert.DoesNotContain(Art(b, "wort"), w => w.Seite == 3 && w.Text() == "R2015-S1");
        // Technische Daten (Schlüssel | Wert) mit umbrochenen Zellen als eine Tabelle
        Assert.Contains(Art(b, "tabelle").Where(x => x.Seite == 1), x => Daten(x).Any(r => r[0] == "Wartung" && r[1] == "wartungsfrei"));
    }

    [Fact]
    public void Flaektgroup_ULSA_ULDA_Seite3_zwei_Tabellen_mit_neun_Groessen()
    {
        var pfad = Echt("familien/ULSA-ULDA-Optivent-Ultra-VAV.pdf");
        if (pfad is null) return;
        var b = Lesen(pfad);
        Assert.Equal(8, b.Seiten);
        var ts = Art(b, "tabelle").Where(x => x.Seite == 3).ToList();
        Assert.Equal(2, ts.Count);
        var ulsa = ts.Single(x => Kopf(x).Length == 5);
        var ulda = ts.Single(x => Kopf(x).Length == 6);
        Assert.Equal(["Größe", "ØD [mm]", "a [mm]", "L [mm]", "Gewicht [kg]"], Kopf(ulsa));
        Assert.Equal(["Größe", "ØD [mm]", "ØDy [mm]", "a [mm]", "L [mm]", "Gewicht [kg]"], Kopf(ulda));
        Assert.Equal(9, Daten(ulsa).Length);
        Assert.Equal(["100", "99", "35", "461", "1,5"], Daten(ulsa)[0]);
        Assert.Equal(["630", "629", "60", "1150", "24,0"], Daten(ulsa)[8]);
        Assert.Equal(["100", "99", "200", "35", "461", "3,1"], Daten(ulda)[0]);
        Assert.Equal(["630", "629", "730", "60", "1150", "41,3"], Daten(ulda)[8]);
        Assert.Equal(1.5, Zahl(Zelle(b, ulsa.Id, 1, 5).Wert));          // Dezimalkomma → Zahl
        Assert.Equal("kg", Zelle(b, ulsa.Id, 1, 5).Einheit);
        // Massbilder (Vektor): Buchstaben L, a, ØD, ØDy und die 70 mm über dem Gehäuse als Kandidaten mit Sicherheit < 1
        var masse = Art(b, "mass").Where(x => x.Seite == 3).ToList();
        foreach (var w in new[] { "L", "a", "ØD", "ØDy", "70" }) Assert.Contains(masse, m => m.Text() == w);
        Assert.All(masse, m => Assert.True(m.Sicherheit < 1));
    }

    [Fact]
    public void Klimaoprema_RVP_P_Seite3_58_Zeilen_H_Gruppen_aus_den_Trennlinien_Seite5_Gehaeuselaenge_350()
    {
        var pfad = Echt("familien/RVP-P.pdf");
        if (pfad is null) return;
        var b = Lesen(pfad);
        Assert.Equal(29, b.Seiten);
        var t = Art(b, "tabelle").Single(x => x.Seite == 3 && Daten(x).Length > 40);
        Assert.Equal(["B [mm]", "H [mm]", "Aef [m²]", "Vmin [m³/h]", "Vmax [m³/h]"], Kopf(t));
        Assert.Equal(58, Daten(t).Length);   // die Tabelle des Datenblatts hat 58 Grössen (nicht 66)
        Assert.Equal(["200", "100", "0,0170", "130", "720"], Daten(t)[0]);
        Assert.Equal(["300", "", "0,0255", "190", "1080"], Daten(t)[1]);   // H gilt für drei Zeilen: nur in der obersten steht der Wert
        Assert.Equal(["1000", "1000", "0,8500", "6265", "36000"], Daten(t)[57]);
        var zus = ((JsonArray)t.Zusatz!["zusammengefasst"]!).Cast<JsonObject>().ToList();
        Assert.All(zus, z => Assert.Equal(2, z["spalte"]!.GetValue<int>()));
        Assert.Equal(3, zus[0]["zeilen"]!.GetValue<int>());
        // Gruppen aus den Trennlinien der Spalte H (nicht aus Füllflächen). Die Linien teilen bei 164.6 pt, eine Zeile nach der Zeile 1000 | 0,5100;
        // die Beschriftungen 600 (195.9 pt) und 700 (143.3 pt) stehen genau in der Mitte der so gezeichneten Gruppen (6 und 3 Zeilen).
        // Die Zeile 700 | 0,4165 liegt damit im PDF in der Gruppe H = 600; nach Aef = 0,85·B·H gehört sie zu H = 700 – das PDF zeichnet
        // die Gruppengrenze eine Zeile zu tief. Der Leser meldet es, wie es steht; der Vergleich mit der Rechnung ist Sache der Quellenprüfung.
        Assert.Equal([3, 3, 5, 4, 8, 7, 7, 6, 6, 3, 3, 2], zus.Select(z => z["zeilen"]!.GetValue<int>()).ToArray());
        Assert.Equal(["100", "150", "200", "250", "300", "350", "400", "500", "600", "700", "800", "900"], zus.Select(z => Daten(t)[z["zeile"]!.GetValue<int>() - 1][1]).ToArray());
        int zeile04165 = Array.FindIndex(Daten(t), r => r[2] == "0,4165");
        var gruppe600 = zus.Single(z => z["zeile"]!.GetValue<int>() - 1 <= zeile04165 && zeile04165 < z["zeile"]!.GetValue<int>() - 1 + z["zeilen"]!.GetValue<int>());
        Assert.Equal("600", Daten(t)[gruppe600["zeile"]!.GetValue<int>() - 1][1]);
        Assert.Equal(6, gruppe600["zeilen"]!.GetValue<int>());
        Assert.Contains(b.Hinweise, h => h.Contains("Seite 3, Tabelle 1") && h.Contains("mehrere Zeilen"));
        var h100 = Zelle(b, t.Id, 1, 2);
        Assert.Equal(100, Zahl(h100.Wert));
        Assert.True(h100.Zusatz!["zusammengefasst"]!.GetValue<bool>());
        Assert.Equal(0.017, Zahl(Zelle(b, t.Id, 1, 3).Wert), 1e-12);
        Assert.Equal("0,0170", Zelle(b, t.Id, 1, 3).Zusatz!["text"]!.GetValue<string>());
        Assert.Equal("m³/h", Zelle(b, t.Id, 1, 5).Einheit);
        // Seite 5: Gehäuselänge 350 mm (die Seite legt zwei Sprachen übereinander, der Leser trennt sie in zwei Ebenen)
        Assert.Contains(Art(b, "zeile"), z => z.Seite == 5 && z.Text() == "Gehäuselänge | 350 mm");
        Assert.Contains(b.Hinweise, h => h.StartsWith("Seite 5:") && h.Contains("übereinanderliegendem Text"));
    }

    [Fact]
    public void Schmidlin_Preisbuch_76_Seiten_unter_60_s_aus_der_Ablage_unter_1_s()
    {
        var pfad = Echt("familien/preisbuecher/preisbuch_kanaele_07_2026.pdf");
        if (pfad is null) return;
        var p = Leer();
        var par = new JsonObject { ["unterlagen"] = new JsonArray(pfad) };
        // erstes Lesen
        var sw = Stopwatch.StartNew();
        var erst = (JsonObject)((JsonObject)Json.Knoten(Tuer.Abfrage(p, "unterlagen_lesen", par)))["unterlagen"]![0]!;
        var zeit1 = sw.Elapsed.TotalSeconds;
        Assert.False(erst["ausAblage"]!.GetValue<bool>());
        Assert.Equal(76, erst["seiten"]!.GetValue<int>());
        Assert.True(zeit1 < 60, $"erstes Lesen {zeit1:0.0} s");
        Assert.True(erst["jeArt"]!["tabelle"]!.GetValue<int>() > 20, "Tabellen: " + erst["jeArt"]!["tabelle"]);
        Assert.True(erst["jeArt"]!.AsObject().Sum(x => x.Value!.GetValue<int>()) > 50_000);
        // zweites Lesen aus der Ablage (Zusammenfassung neben den Befunden)
        sw.Restart();
        var zweit = (JsonObject)((JsonObject)Json.Knoten(Tuer.Abfrage(p, "unterlagen_lesen", par)))["unterlagen"]![0]!;
        Assert.True(zweit["ausAblage"]!.GetValue<bool>());
        Assert.True(sw.Elapsed.TotalSeconds < 1, $"unterlagen_lesen aus der Ablage {sw.Elapsed.TotalSeconds:0.00} s");
        Assert.Equal(erst["befunde"]!.GetValue<int>(), zweit["befunde"]!.GetValue<int>());
        Assert.Equal(erst["tabellen"]!.AsArray().Count, zweit["tabellen"]!.AsArray().Count);
        // die Befunde selbst: aus dem Speicher sofort, von der Platte (Neustart) in wenigen Sekunden
        var u = UnterlagenService.Aufloesen(p, pfad);
        var mem = UnterlagenService.Lesen(p, u);
        Assert.True(mem.AusAblage && mem.Sekunden < 1, $"Befunde aus dem Speicher {mem.Sekunden:0.00} s");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        sw.Restart();
        var neu = UnterlagenService.Laden(mem.AblageDatei)!;
        Assert.True(sw.Elapsed.TotalSeconds < 10, $"Befunde von der Platte {sw.Elapsed.TotalSeconds:0.0} s");
        Assert.Equal(mem.Befunde.Befunde.Count, neu.Befunde.Count);
        Assert.Equal(mem.Befunde.Befunde.Count(x => x.Art == "zelle"), neu.Befunde.Count(x => x.Art == "zelle"));
    }
}
