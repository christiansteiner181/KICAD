using System.Text.Json.Nodes;
using Hlks.Tests.Gemeinsam;

namespace Hlks.Kern.Tests;

/// <summary>Modul 3c, Teil K: Normensuche (Normenverzeichnis, Abfragen normen_suchen und norm_lesen) mit den eigenen Test-PDF aus
/// tests/daten/normen (erfundener Text, keine echte Norm). Jeder Test kopiert sie in einen eigenen Ordner unter %TEMP% – nie W:.</summary>
public sealed class NormenverzeichnisTests : IDisposable
{
    readonly string wurzel = NormenTestdaten.Kopie();
    readonly string ablage = Path.Combine(Path.GetTempPath(), "hlks_normen_ablage_" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        NormenTestdaten.Loeschen(wurzel);
        NormenTestdaten.Loeschen(ablage);
    }

    Normenverzeichnis V() => Normenverzeichnis.Fuer(wurzel, ablage);

    [Fact]
    public void Verzeichnis_NurDeutscheFassungen_ReiheUndKuerzel()
    {
        var l = V().Verzeichnis();
        Assert.Equal(2, l.Count);                                       // die französische Fassung (_f) fehlt
        var sia = Assert.Single(l, e => e.Reihe == "SIA");
        Assert.Equal("SIA 9999:2026", sia.Kuerzel);
        Assert.Equal(Path.Combine("SIA_Normen", "9999_2026_d.pdf"), sia.Datei);
        Assert.StartsWith("N-", sia.Id);
        var mb = Assert.Single(l, e => e.Reihe == "Merkblatt");
        Assert.Equal("Testmerkblatt_Pruefleitungen_2025.pdf", mb.Datei);
        Assert.DoesNotContain(l, e => e.Datei.EndsWith("_f.pdf"));
    }

    [Fact]
    public void Suchen_FindetSeiteZifferAusgabeUndAuszug()
    {
        var (treffer, vollstaendig) = V().Suchen("Mindestgefälle Prüfleitung");
        Assert.True(vollstaendig);
        var t = treffer[0];
        Assert.Equal("SIA 9999:2026", t.Norm);
        Assert.Equal("2026", t.Ausgabe);
        Assert.Equal(3, t.Seite);
        Assert.Equal("4.2.1", t.Ziffer);
        Assert.Equal(Path.Combine("SIA_Normen", "9999_2026_d.pdf"), t.Datei);
        Assert.Contains("7 Promille", t.Auszug);
        Assert.True(t.Auszug.Length <= Normenverzeichnis.AuszugMax, $"Auszug {t.Auszug.Length} Zeichen");
        Assert.DoesNotContain(treffer, x => x.Datei.EndsWith("_f.pdf"));
    }

    [Fact]
    public void Suchen_AuszugHoechstens600Zeichen_AufLangerSeite()
    {
        var (treffer, _) = V().Suchen("Fülltext Länge");
        Assert.NotEmpty(treffer);
        Assert.All(treffer, t => Assert.True(t.Auszug.Length <= 600, $"{t.Auszug.Length} Zeichen"));
        Assert.Contains(treffer, t => t.Auszug.Length > 400);       // die Seite ist lang: der Auszug wird ausgenutzt, aber begrenzt
    }

    [Fact]
    public void Suchen_FrageAlsSatz_FuellwoerterWerdenUebergangen()
    {
        Assert.Equal(["Mindestgefälle", "Prüfleitung"], Normenverzeichnis.Begriffe("Wie gross ist das Mindestgefälle der Prüfleitung?"));
        var (treffer, vollstaendig) = V().Suchen("Wie gross ist das Mindestgefälle der Prüfleitung?");
        Assert.True(vollstaendig);
        Assert.Equal(3, treffer[0].Seite);
    }

    [Fact]
    public void Suchen_Reihe_Eingrenzen()
    {
        var alle = V().Suchen("Spülung Prüfleitung").Treffer;
        Assert.Contains(alle, t => t.Reihe == "SIA" && t.Seite == 4);
        Assert.Contains(alle, t => t.Reihe == "Merkblatt");
        var nurMerkblatt = V().Suchen("Spülung Prüfleitung", reihe: "Merkblatt").Treffer;
        Assert.All(nurMerkblatt, t => Assert.Equal("Merkblatt", t.Reihe));
        Assert.NotEmpty(nurMerkblatt);
        Assert.Equal("2025", nurMerkblatt[0].Ausgabe);                  // Jahreszahl aus dem Dateinamen
    }

    [Fact]
    public void Suchen_EinBegriffFehlt_RueckfallMitHinweis()
    {
        var (treffer, vollstaendig) = V().Suchen("Mindestgefälle Prüfleitung Gibtsnichtwort");
        Assert.False(vollstaendig);
        Assert.Contains(treffer, t => t.Seite == 3);
        var json = V().SuchenJson("Mindestgefälle Prüfleitung Gibtsnichtwort");
        Assert.False(json["vollstaendig"]!.GetValue<bool>());
        Assert.Contains("bis auf einen", json["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public void Suchen_KeinTreffer_Hinweis()
    {
        var json = V().SuchenJson("Gibtsnichtwort Auchnicht");
        Assert.Empty(json["treffer"]!.AsArray());
        Assert.Contains("Keine", json["hinweis"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => V().SuchenJson("  "));
    }

    [Fact]
    public void Lesen_SeitenMitKopfUndGrenzeDreiSeiten()
    {
        var v = V();
        var text = v.Lesen("SIA_Normen/9999_2026_d.pdf", 3);
        Assert.StartsWith("SIA 9999:2026", text);
        Assert.Contains("Ausgabe 2026", text);
        Assert.Contains("=== Seite 3 ===", text);
        Assert.Contains("7 Promille", text);
        Assert.DoesNotContain("=== Seite 4 ===", text);

        var zwei = v.Lesen(@"SIA_Normen\9999_2026_d.pdf", 3, 9);      // bis über das Ende: bis zur letzten Seite
        Assert.Contains("=== Seite 4 ===", zwei);

        var drei = v.Lesen("SIA 9999:2026", 1, 4);                       // über das Kürzel; 4 Seiten verlangt, 3 geliefert
        Assert.Contains("=== Seite 1 ===", drei);
        Assert.Contains("=== Seite 3 ===", drei);
        Assert.DoesNotContain("=== Seite 4 ===", drei);
        Assert.Contains("höchstens 3 Seiten", drei);

        var id = v.Verzeichnis().Single(e => e.Reihe == "SIA").Id;
        Assert.Contains("=== Seite 2 ===", v.Lesen(id, 2));
    }

    [Fact]
    public void Lesen_NurEintraegeDesVerzeichnisses_SonstFehler()
    {
        var v = V();
        Assert.Throws<HlksFehler>(() => v.Lesen(@"..\..\Windows\win.ini", 1));
        Assert.Throws<HlksFehler>(() => v.Lesen(Path.Combine(wurzel, "SIA_Normen", "9999_2026_f.pdf"), 1));   // französisch: nicht im Verzeichnis
        var f = Assert.Throws<HlksFehler>(() => v.Lesen("SIA 9999:2026", 9));
        Assert.Contains("Seiten 1 bis 4", f.Message);
    }

    [Fact]
    public void Zwischenspeicher_AusserhalbDerWurzel_WurzelUnveraendert()
    {
        var vorher = Directory.GetFiles(wurzel, "*", SearchOption.AllDirectories).Order().ToArray();
        V().Suchen("Prüfleitung");
        Assert.Equal(vorher, Directory.GetFiles(wurzel, "*", SearchOption.AllDirectories).Order().ToArray());   // nur lesen
        Assert.NotEmpty(Directory.GetFiles(ablage, "N-*.txt"));
        Assert.True(File.Exists(Path.Combine(ablage, "verzeichnis.json")));
    }

    [Fact]
    public void Vorgabe_ZwischenspeicherImTestlaufUnterTemp()
    {
        var v = Normenverzeichnis.Fuer(wurzel);
        Assert.StartsWith(Path.Combine(Path.GetTempPath(), "HLKS_CAD", "normen_test"), v.Ablage, StringComparison.OrdinalIgnoreCase);
        Assert.Null(v.Sperre);
    }

    [Fact]
    public void Testlauf_WurzelAufW_WirdNichtGelesen()
    {
        var v = Normenverzeichnis.Fuer(Normenverzeichnis.VorgabeWurzel);
        Assert.NotNull(v.Sperre);
        Assert.Contains("Testlauf", v.Sperre);
        Assert.Empty(v.Verzeichnis());
        var json = v.SuchenJson("Mindestgefälle");
        Assert.Empty(json["treffer"]!.AsArray());
        Assert.Contains("Testlauf", json["hinweis"]!.GetValue<string>());
        Assert.Throws<HlksFehler>(() => v.Lesen("SIA 2024:2021", 1));
        Assert.False(v.ZustandJson()["verfuegbar"]!.GetValue<bool>());
    }

    [Fact]
    public void GeaenderteDatei_WirdNeuGelesen()
    {
        var v = V();
        Assert.Contains("7 Promille", v.Lesen("SIA 9999:2026", 3));
        var datei = Path.Combine(wurzel, "SIA_Normen", "9999_2026_d.pdf");
        File.Copy(Path.Combine(wurzel, "Testmerkblatt_Pruefleitungen_2025.pdf"), datei, overwrite: true);
        File.SetLastWriteTimeUtc(datei, DateTime.UtcNow.AddMinutes(1));
        v.Verzeichnis(neu: true);
        Assert.Contains("Merkblatt Prüfleitungen", v.Lesen("SIA_Normen/9999_2026_d.pdf", 1));
    }

    [Fact]
    public async Task Aufbauen_LiestAlleVolltexte()
    {
        var v = V();
        await v.AufbauenImHintergrund();
        var z = v.ZustandJson();
        Assert.Equal(2, z["normen"]!.GetValue<int>());
        Assert.Equal(2, z["mitVolltext"]!.GetValue<int>());
        Assert.True(z["verfuegbar"]!.GetValue<bool>());
        Assert.False(z["aufbau"]!["laeuft"]!.GetValue<bool>());
    }
}

/// <summary>Normensuche über die Umgebungsvariable HLKS_NORMEN und die Tür (Tuer.Abfrage) – setzt eine Prozessvariable, darum allein.</summary>
[CollectionDefinition("NormenUmgebung", DisableParallelization = true)]
public sealed class NormenUmgebungSammlung { }

[Collection("NormenUmgebung")]
public sealed class NormenUmgebungTests : IDisposable
{
    readonly string wurzel = NormenTestdaten.Kopie();
    public void Dispose() => NormenTestdaten.Loeschen(wurzel);

    [Fact]
    public void Umgebungsvariable_BestimmtDieWurzel_TuerLiefertTreffer()
    {
        using var u = TestUmgebung.Setzen(("HLKS_NORMEN", wurzel));
        Assert.Equal(wurzel, Normenverzeichnis.WurzelAusUmgebung());
        var p = Projekt.Anlegen(Katalog.Laden(Katalog.StandardOrdner()), null, "Normen", "T", "pc");
        var json = (JsonObject)Tuer.Abfrage(p, "normen_suchen", new JsonObject { ["frage"] = "Mindestgefälle Prüfleitung" });
        var t = json["treffer"]!.AsArray()[0]!;
        Assert.Equal("SIA 9999:2026", t["norm"]!.GetValue<string>());
        Assert.Equal(3, t["seite"]!.GetValue<int>());
        Assert.Equal("4.2.1", t["ziffer"]!.GetValue<string>());
        var text = (string)Tuer.Abfrage(p, "norm_lesen", new JsonObject { ["datei"] = t["datei"]!.GetValue<string>(), ["seite"] = 3 });
        Assert.Contains("7 Promille", text);
        Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "norm_lesen", new JsonObject { ["datei"] = "SIA 9999:2026" }));   // seite fehlt
        Assert.Contains(Abfragen.Liste, a => a.Name == "normen_suchen");
        Assert.Contains(Abfragen.Liste, a => a.Name == "norm_lesen");
    }

    [Fact]
    public void OhneUmgebungsvariable_VorgabeW()
    {
        using var u = TestUmgebung.Setzen(("HLKS_NORMEN", null));
        Assert.Equal(@"W:\05_Normen und Merkblätter", Normenverzeichnis.WurzelAusUmgebung());
        Assert.NotNull(Normenverzeichnis.Standard().Sperre);          // im Testlauf nie gelesen
    }
}

/// <summary>Kopie der Test-PDF (im Ausgabeordner unter normen\, aus tests/daten/normen) in einen eigenen Ordner unter %TEMP%.</summary>
public static class NormenTestdaten
{
    public static string Kopie()
    {
        var quelle = Path.Combine(AppContext.BaseDirectory, "normen");
        if (!Directory.Exists(quelle)) throw new DirectoryNotFoundException("Test-PDF fehlen: " + quelle);
        var ziel = Path.Combine(Path.GetTempPath(), "hlks_normen_" + Guid.NewGuid().ToString("N")[..10]);
        foreach (var f in Directory.GetFiles(quelle, "*.pdf", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(quelle, f);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(ziel, rel))!);
            File.Copy(f, Path.Combine(ziel, rel));
        }
        return ziel;
    }

    public static void Loeschen(string ordner)
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); return; }
            catch (IOException) { Thread.Sleep(200); }
            catch (UnauthorizedAccessException) { Thread.Sleep(200); }
        }
    }
}
