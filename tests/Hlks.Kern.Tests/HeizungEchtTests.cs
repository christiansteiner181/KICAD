using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Hlks.Kern.Tests;

/// <summary>Modul 5 Teil A-D, Referenzfall Bernstrasse 42 (H+K Revit 2025, IFC2x3; lokale Testkopie %USERPROFILE%\HLKS_CAD_testdaten, nur lesen,
/// nie im Repo). Fehlt die Datei, kehrt der Test früh zurück (wie IfcHlksEchtTests). Erwartung aus dem Auftrag: nach Import und
/// heizgruppen_vorschlagen die Heizgruppe «FBH gemischt» mit 3-Weg-Ventil, Pumpe und 2 Thermometern, Verteilung im 1. UG, 45 FBH-Kreise,
/// Strang A EG 6 / 1.OG 7 / 2.OG 7 / Attika 4 und Strang B 5/5/5/6.
/// Geschoss der FBH-Verteiler: das letzte Geschoss mit Kote ≤ z + 200 mm – der Einfügepunkt der Revit-Verteiler liegt rund 110–150 mm unter der
/// Geschosskote (in der Rohdecke), die IFC-Zuordnung (Containment) nennt deshalb für 1.OG, 2.OG und Attika jeweils das Geschoss darunter.
/// Bekannte Modellmängel erscheinen als Befund: Ausdehnungsgefäss ohne Anschluss (P01), Bypass des Mischventils offen (P35), FBH-Kreise mit
/// Vorlauf im System «H VL HT» (P33); dazu ist im Modell die Fliessrichtung der Gruppe verkehrt (Mischventil über ab erreicht, P35).</summary>
public sealed class HeizungEchtTests(ITestOutputHelper aus)
{
    [Fact]
    public void Bernstrasse42_HeizgruppenVorschlagen_FbhGemischtMit45Kreisen()
    {
        if (IfcHilfe.Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } datei) return;
        var p = IfcHilfe.Leer();
        try
        {
            IfcHilfe.Ok(IfcHilfe.Uebernehmen(p, datei, new JsonObject { ["bereiche"] = new JsonArray("hlks"), ["grundlage"] = false }));
            var m = p.Modell; var k = p.Katalog;
            // Systempaar aus den Namen beim Import (H VL NT ↔ H RL NT); H VL HT ohne Gegenstück
            var vlNt = m.DerKlasse("System").Single(s => s.Kennzeichen == "H-VL-NT");
            Assert.Equal("H-RL-NT", Heizgruppen.Gegenstueck(m, vlNt)?.Kennzeichen);
            Assert.Null(Heizgruppen.Gegenstueck(m, m.DerKlasse("System").Single(s => s.Kennzeichen == "H-VL-HT")));

            var e = IfcHilfe.Ok(p.Ausfuehren("heizgruppen_vorschlagen", new JsonObject()));
            foreach (var h in e.Hinweise) aus.WriteLine(h);
            aus.WriteLine(HeizungAbfragen.Lesen(p, null));

            var hg = Assert.Single(m.DerKlasse("Heizgruppe"));
            Assert.Equal(("FBH gemischt", "gemischt", "fbh"), (hg.Name, hg.Text("art"), hg.Text("verbraucherart")));
            var w = Heizgruppen.Ergebnis(m, k).Gruppen[hg.Id];
            Assert.True(w.Systempaar);
            Assert.Equal(("H-VL-NT", "H-RL-NT"), (w.Vorlauf?.Kennzeichen, w.Ruecklauf?.Kennzeichen));
            // 3-Weg-Mischventil, Pumpe, 2 Thermometer (Anlegefühler über bezuege.an, je einer im Vor- und Rücklauf)
            var mv = Assert.Single(w.Mischer);
            Assert.Equal(("Mischventil", "3weg"), (mv.Klasse, mv.Text("art")));
            Assert.Single(w.Pumpen);
            var tf = w.Glieder.Where(g => g.Element.Klasse == "Fuehler").ToList();
            Assert.Equal(2, tf.Count);
            Assert.All(tf, g => Assert.Equal(("thermometer", "an"), (g.Element.Text("art"), g.Eintritt)));
            Assert.Equal(["rl", "vl"], tf.Select(g => g.Weg).Order());

            // 45 FBH-Kreise in 8 Unterverteilern, 2 Äste ab dem Abzweig
            Assert.Equal(45, w.Verbraucher.Count);
            Assert.All(w.Verbraucher, v => Assert.Equal("Fussbodenheizkreis", v.Klasse));
            Assert.Equal(8, w.Unterverteiler.Count);
            Assert.Equal(2, w.Aeste.Count);
            Assert.Equal(45, w.Aeste.Sum(a => a.Verbraucher.Count));
            var geschosse = m.DerKlasse("Geschoss").Where(g => g.Zahl("kote") is not null).OrderBy(g => g.Zahl("kote")).ToList();
            string Gs(Element x) => geschosse.Last(g => g.Zahl("kote") <= x.Geometrie!.Punkte[0][2] + 200).Kennzeichen;
            var aeste = w.Aeste.Select(a => string.Join(" ", a.Unterverteiler.OrderBy(u => u.Geometrie!.Punkte[0][2]).Select(u => $"{Gs(u)}:{w.KreiseVon(u).Count}"))).Order().ToList();
            aus.WriteLine(string.Join(" | ", aeste));
            Assert.Equal(["EG:5 1.OG:5 2.OG:5 AT:6", "EG:6 1.OG:7 2.OG:7 AT:4"], aeste);   // Strang B, Strang A
            // Verteilung im 1. UG: die Absperrventile der beiden Äste (je Vor- und Rücklauf)
            var astVentile = w.Glieder.Where(g => g.Ast is not null && g.Element.Klasse == "Ventil").ToList();
            Assert.Equal(4, astVentile.Count);
            Assert.All(astVentile, g => Assert.Equal("1.UG", Gs(g.Element)));
            Assert.Equal([1, 1, 2, 2], astVentile.Select(g => g.Ast!.Value).Order());

            // bekannte Modellmängel als Befund
            var b = Pruefungen.Alle(m, k);
            var ag = Assert.Single(m.DerKlasse("Ausdehnungsgefaess"));
            Assert.Contains(b, x => x.Code == "P01" && x.Element == ag.Kennzeichen);
            Assert.Contains(b, x => x.Code == "P35" && x.Element == mv.Kennzeichen && x.Text.Contains("b (Bypass) ist offen"));
            Assert.Contains(b, x => x.Code == "P35" && x.Element == mv.Kennzeichen && x.Text.Contains("a und ab vertauscht"));
            Assert.True(b.Count(x => x.Code == "P33" && x.Element.StartsWith("FBH") && x.Text.Contains("H-VL-HT")) >= 10);
            Assert.Contains(b, x => x.Code == "P36" && x.Element == hg.Kennzeichen && x.Schwere == "hinweis");   // Temperaturen: Eingabe fehlt
        }
        finally
        {
            try { Directory.Delete(Unterlagen.Ordner(p), true); } catch (IOException) { }
        }
    }
}
