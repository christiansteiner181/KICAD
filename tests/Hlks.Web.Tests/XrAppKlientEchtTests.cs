using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Hlks.Xr;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using Xunit.Abstractions;

namespace Hlks.Web.Tests;

/// <summary>
/// Quest App P4a am realen Projekt Test_praxis (Christian 23.10.2026: «für tests im echten projekt das test_praxis projekt verwenden»): KICAD-Server mit einer
/// Arbeitskopie von %USERPROFILE%\HLKS_CAD_testdaten\test_praxis (unter %TEMP%; nichts davon ins Repository), die App-Seite (Hlks.Xr.Netz) koppelt, lädt alle
/// Geschosse mit Unterlage (Zeit je Geschoss und Teil, Grösse im Zwischenspeicher), lädt erneut bedingt (304) und ohne PC, baut die Szene und misst den
/// Durchgang beim Gehen durch das EG. Ohne die Kopie kehrt der Test ohne Prüfung zurück (Muster XrModellEchtTests aus P2).
/// Messwerte in der Testausgabe und in %TEMP%\HLKS_CAD\xr_p4a_test_praxis.json.
/// </summary>
public class XrAppKlientEchtTests(ITestOutputHelper aus)
{
    static string Quelle => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", "test_praxis");

    static void Kopieren(string von, string nach)
    {
        Directory.CreateDirectory(nach);
        foreach (var d in Directory.GetDirectories(von, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(nach, Path.GetRelativePath(von, d)));
        foreach (var f in Directory.GetFiles(von, "*", SearchOption.AllDirectories)) File.Copy(f, Path.Combine(nach, Path.GetRelativePath(von, f)), true);
    }

    [Fact]
    public async Task TestPraxis_App_laedt_alle_Geschosse_mit_Unterlage_304_ohne_PC_Szene_und_Durchgang()
    {
        if (!File.Exists(Path.Combine(Quelle, "projekt.json"))) { aus.WriteLine($"Echte Daten fehlen: {Quelle} – Test ohne Prüfung beendet."); return; }
        await using var s = new XrServer();
        var (port, _, _) = await s.Aktiv();
        var ordner = Path.Combine(s.Ordner, "test_praxis");
        var uhr = Stopwatch.StartNew();
        Kopieren(Quelle, ordner);
        var kopieren = uhr.ElapsedMilliseconds;
        uhr.Restart();
        var (code, j) = await s.LokalPost("/api/projekt/oeffnen", new { ordner });
        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        var oeffnen = uhr.ElapsedMilliseconds;

        var paket = Kopplungspaket.Lesen((await s.Paket("QUEST-ECHT")).ToJsonString());
        var f = await PcSuche.Finden(paket, null, new SucheOptionen { Suchen = false });
        Assert.True(f.Zustand == PcFundZustand.Gefunden, f.Meldung);
        using var zugang = new PcZugang(f.Adresse!, paket);
        IModellQuelle quelle = zugang.Modell;
        try { await quelle.GeschosseText(CancellationToken.None); }
        catch (XrNetzFehler e) when (e.Message.Contains("(404)", StringComparison.Ordinal))
        {
            aus.WriteLine("Modell über localhost: der Netzzugang gibt die GLB-Endpunkte noch nicht frei (404).");
            quelle = new ModellHttp(s.Lokal, "lokal");
        }

        // ---------------------------------------------------------------- erstes Laden (der Server erzeugt die GLB), dann bedingt, dann ohne PC
        var sp = new GlbZwischenspeicher(Path.Combine(s.Ordner, "app"));
        uhr.Restart();
        var stand = await ModellLader.VomPc(quelle, sp, new ModellLadeOptionen { Pc = paket.Pc });
        var erstesMs = uhr.ElapsedMilliseconds;
        Assert.Empty(stand.Hinweise);
        Assert.All(stand.Teile, t => Assert.Equal(GlbQuelle.Geladen, t.Quelle));
        Assert.Equal(stand.Liste.Geschosse.Count + stand.Liste.Geschosse.Count(g => g.Unterlage is not null), stand.Teile.Count);
        Assert.Contains(stand.Teile, t => t.Teil == GlbZwischenspeicher.TeilUnterlage);
        uhr.Restart();
        var bedingt = await ModellLader.VomPc(quelle, sp, new ModellLadeOptionen { Bedingt = true });
        var bedingtMs = uhr.ElapsedMilliseconds;
        Assert.All(bedingt.Teile, t => Assert.Equal(GlbQuelle.Unveraendert, t.Quelle));
        uhr.Restart();
        var ohnePc = ModellLader.AusZwischenspeicher(sp)!;
        var ohnePcMs = uhr.ElapsedMilliseconds;
        Assert.Equal(stand.Teile.Select(t => t.Glb.Length), ohnePc.Teile.Select(t => t.Glb.Length));
        var belegt = sp.Belegt();

        // ---------------------------------------------------------------- Szene, Budget der Quest 3, Start
        uhr.Restart();
        var szene = ModellSzene.AusGlb(stand.Teile.Select(t => (t.Glb, t.Geschoss.Id, t.Teil, $"{t.Geschoss.Kennzeichen} {t.Teil}")));
        var szeneMs = uhr.ElapsedMilliseconds;
        Assert.Empty(szene.Hinweise);
        var (netze, dreiecke) = szene.Sichtbar(new HashSet<string> { ModellSzene.GruppeRaum });
        Assert.True(netze < 200, $"{netze} Netze");                 // Meta, Quest 3: höchstens ~200 Zeichenaufrufe je Bild (modul-xr.md, «Prüfung der Technik»)
        Assert.True(dreiecke < 1_500_000, $"{dreiecke} Dreiecke");   // … und 1.5 Mio. Dreiecke
        Assert.NotEmpty(szene.Raeume);
        Assert.Contains(szene.DurchgangsBauteile, e => e.Teil == GlbZwischenspeicher.TeilUnterlage);   // IFC-Wände der Unterlage (Annahme P4a)
        uhr.Restart();
        var start = await AppStart.Waehlen(null, null, szene, stand.Liste);
        var startMs = uhr.ElapsedMilliseconds;
        Assert.Equal(Startquellen.Eg, start.Punkt.Quelle);
        var eg = stand.Liste.Geschosse.Single(g => g.Kennzeichen == "EG");
        Assert.Equal(eg.Id, szene.Finde(start.Punkt.Standort!.Raum!)!.GeschossId);

        // ---------------------------------------------------------------- Durchgang beim Gehen: 10 m geradeaus in 1-cm-Schritten auf Augenhöhe
        var z = start.Zustand;
        var verfolger = new DurchgangVerfolger(szene);
        int wechsel = 0, maxNah = 0, maxGemessen = 0;
        double maxMs = 0;
        uhr.Restart();
        for (int i = 0; i <= 1000; i++)
        {
            var t0 = uhr.Elapsed.TotalMilliseconds;
            var kopf = new Hlks.Xr.Vec3(z.X + Math.Cos(z.Gieren) * i * 0.01, z.Boden + z.Augen, z.Z - Math.Sin(z.Gieren) * i * 0.01);
            if (verfolger.Pruefen(kopf)) wechsel++;
            maxMs = Math.Max(maxMs, uhr.Elapsed.TotalMilliseconds - t0);
            maxNah = Math.Max(maxNah, verfolger.Nah.Count);
            maxGemessen = Math.Max(maxGemessen, verfolger.Gemessen);
        }
        var gehenMs = uhr.Elapsed.TotalMilliseconds;
        Assert.True(wechsel > 0, "auf 10 m geradeaus kommt man an keiner Wand vorbei");

        // ---------------------------------------------------------------- Zahlen
        aus.WriteLine($"Test_praxis: Kopieren {kopieren} ms, Öffnen {oeffnen} ms; erstes Laden {erstesMs} ms, bedingt (304) {bedingtMs} ms, ohne PC {ohnePcMs} ms; " +
            $"Zwischenspeicher {belegt / 1048576.0:F1} MB; Szene {szeneMs} ms; Start {startMs} ms");
        aus.WriteLine($"{"Geschoss",-9} {"Teil",-9} {"ms",6} {"MB",7} {"Dreiecke",9}");
        var zeilen = new JsonArray();
        foreach (var t in stand.Teile)
        {
            var dr = szene.Netze.Where(n => n.GeschossId == t.Geschoss.Id && n.Teil == t.Teil).Sum(n => (long)n.Daten.Dreiecke);
            aus.WriteLine($"{t.Geschoss.Kennzeichen,-9} {t.Teil,-9} {t.Ms,6:F0} {t.Glb.Length / 1048576.0,7:F2} {dr,9}");
            zeilen.Add(new JsonObject { ["geschoss"] = t.Geschoss.Kennzeichen, ["teil"] = t.Teil, ["ms"] = Math.Round(t.Ms), ["mb"] = Math.Round(t.Glb.Length / 1048576.0, 3), ["dreiecke"] = dr });
        }
        aus.WriteLine($"Sichtbar (ohne Räume): {netze} Netze, {dreiecke} Dreiecke; {szene.Raeume.Count} Räume, {szene.DurchgangsBauteile.Count} Bauteile für den Durchgang");
        aus.WriteLine($"Durchgang über 10 m (1001 Kopfpositionen): {wechsel} Wechsel, höchstens {maxNah} Bauteile nah, höchstens {maxGemessen} genau gemessen, " +
            $"im Mittel {gehenMs / 1001:F3} ms, höchstens {maxMs:F2} ms je Position");
        var datei = Path.Combine(Path.GetTempPath(), "HLKS_CAD", "xr_p4a_test_praxis.json");
        Directory.CreateDirectory(Path.GetDirectoryName(datei)!);
        File.WriteAllText(datei, new JsonObject
        {
            ["oeffnenMs"] = oeffnen, ["erstesLadenMs"] = erstesMs, ["bedingtMs"] = bedingtMs, ["ohnePcMs"] = ohnePcMs, ["zwischenspeicherMb"] = Math.Round(belegt / 1048576.0, 2),
            ["szeneMs"] = szeneMs, ["netze"] = netze, ["dreiecke"] = dreiecke, ["raeume"] = szene.Raeume.Count, ["durchgangBauteile"] = szene.DurchgangsBauteile.Count,
            ["durchgangMittelMs"] = Math.Round(gehenMs / 1001, 4), ["durchgangMaxMs"] = Math.Round(maxMs, 3), ["teile"] = zeilen,
        }.ToJsonString(Json.Eingerueckt));
        // Durchgang je Bild: unter 1 ms im Mittel am PC (die Quest ist langsamer; Bildbudget bei 72 Hz 13.9 ms)
        Assert.True(gehenMs / 1001 < 1, $"Durchgang im Mittel {gehenMs / 1001:F3} ms");
    }
}
