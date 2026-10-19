using System.Text.Json;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>
/// Liste «zuletzt geöffnet» je Benutzer (S1, entwurf/startbildschirm.md): je Rechner eine Datei, Vereinigung über alle Rechner,
/// Anheften und Entfernen mit Zeit (der neuere Vermerk gilt), Übernahme der alten Liste, Schutz vor dem Beschreiben der echten Liste.
/// Alle Ordner liegen unter %TEMP% (eigener Ordner je Test); die echte Liste des Benutzers wird nie berührt.
/// </summary>
public class ZuletztListeTests : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks_zuletzt_" + Guid.NewGuid().ToString("N")[..10]);
    DateTimeOffset jetzt = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    public ZuletztListeTests() => Directory.CreateDirectory(wurzel);

    public void Dispose()
    {
        for (int i = 0; i < 5; i++)
        {
            try { if (Directory.Exists(wurzel)) Directory.Delete(wurzel, true); return; }
            catch (IOException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { Thread.Sleep(100); }
        }
    }

    ZuletztListe Rechner(string name, string? alt = null, string? ordner = null) =>
        new(ordner ?? Path.Combine(wurzel, "zuletzt"), name, true, alt, false, "Test") { Zwischenspeicher = TimeSpan.Zero, Uhr = () => jetzt };

    void Weiter(int minuten) => jetzt = jetzt.AddMinutes(minuten);

    const string P1 = @"W:\02_Projektdateien Objekte\Worb_BES_Bernstrasse\c_Pläne und Modelle\e_KICAD";
    const string P2 = @"W:\02_Projektdateien Objekte\Bern_ARK_Murtenstrasse\c_Pläne und Modelle\e_KICAD";
    const string P3 = @"W:\02_Projektdateien Objekte\Thun_OST_Ostring\c_Pläne und Modelle\e_KICAD";

    // ------------------------------------------------------------------ Vereinigen (reine Funktion)

    static ZuletztDatei Datei(string rechner, params ZuletztEintrag[] e) => new() { Rechner = rechner, Eintraege = [.. e] };
    static ZuletztEintrag E(string ordner, string name, DateTimeOffset? zeit = null, bool? pin = null, DateTimeOffset? pinZeit = null, DateTimeOffset? entfernt = null) =>
        new() { Ordner = ordner, Name = name, Zeit = zeit, Angeheftet = pin, AngeheftetZeit = pinZeit, EntferntZeit = entfernt };
    static DateTimeOffset T(int minuten) => new DateTimeOffset(2026, 10, 19, 8, 0, 0, TimeSpan.Zero).AddMinutes(minuten);

    [Fact]
    public void Vereinigen_JeProjektDerNeuesteEintragUeberAlleRechner()
    {
        var l = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse alt", T(10)), E(P2, "Murtenstrasse", T(5))),
            Datei("B", E(P1, "Bernstrasse neu", T(30)))]);
        Assert.Equal(2, l.Count);
        Assert.Equal(P1, l[0].Ordner);
        Assert.Equal("Bernstrasse neu", l[0].Name);   // Name vom neuesten Eintrag
        Assert.Equal("B", l[0].Rechner);              // und der Rechner, auf dem es zuletzt geöffnet wurde
        Assert.Equal(T(30), l[0].Zeit);
        Assert.Equal("Murtenstrasse", l[1].Name);
        Assert.Equal("A", l[1].Rechner);
    }

    [Fact]
    public void Vereinigen_OrdnerGleichOhneBeachtungVonSchreibweiseUndSchlusstrenner()
    {
        var l = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(10))),
            Datei("B", E(P1.ToLowerInvariant() + @"\", "Bernstrasse", T(20)))]);
        Assert.Single(l);
        Assert.Equal("B", l[0].Rechner);
    }

    [Fact]
    public void Vereinigen_AngeheftetesProjektIstAufAllenRechnernAngeheftetUndStehtZuerst()
    {
        var l = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(10), true, T(11)), E(P2, "Murtenstrasse", T(50))),
            Datei("B", E(P2, "Murtenstrasse", T(60)), E(P3, "Ostring", T(70)))]);
        Assert.Equal([P1, P3, P2], l.Select(z => z.Ordner));        // angeheftet zuerst, dann nach Zeit
        Assert.True(l[0].Angeheftet);
        Assert.False(l[1].Angeheftet);
    }

    [Fact]
    public void Vereinigen_NeuererVermerkGewinnt_AnheftenUndLoesenAufVerschiedenenRechnern()
    {
        // A heftet an (T11), B löst später (T40): gelöst
        var geloest = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(10), true, T(11))),
            Datei("B", E(P1, "Bernstrasse", T(5), false, T(40)))]);
        Assert.False(geloest.Single().Angeheftet);
        // B löst (T40), A heftet danach wieder an (T50): angeheftet
        var wieder = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(10), true, T(50))),
            Datei("B", E(P1, "Bernstrasse", T(5), false, T(40)))]);
        Assert.True(wieder.Single().Angeheftet);
    }

    [Fact]
    public void Vereinigen_EntferntBleibtVerborgen_BisDasProjektWiederGeoeffnetWird()
    {
        var verborgen = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(10))),
            Datei("B", E(P1, "Bernstrasse", null, null, null, T(20)))]);
        Assert.Empty(verborgen);
        var wieder = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(30))),
            Datei("B", E(P1, "Bernstrasse", null, null, null, T(20)))]);
        Assert.Single(wieder);
    }

    [Fact]
    public void Vereinigen_EntferntesAngeheftetesProjektVerschwindetAuchAnDerAnheftung()
    {
        var l = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Bernstrasse", T(10), true, T(11))),
            Datei("B", E(P1, "Bernstrasse", null, false, T(40), T(40)))]);
        Assert.Empty(l);
    }

    [Fact]
    public void Vereinigen_NurAngeheftetOhneJeGeoeffnetZuSein_IstSichtbar_NurGeloestOderEntferntNicht()
    {
        var l = ZuletztListe.Vereinigen([
            Datei("A", E(P1, "Nur angeheftet", null, true, T(10)), E(P2, "Nur gelöst", null, false, T(10)), E(P3, "Nur entfernt", null, null, null, T(10)))]);
        Assert.Equal([P1], l.Select(z => z.Ordner));
        Assert.Equal(T(10), l[0].Zeit);
    }

    [Fact]
    public void Vereinigen_OrdnerUnterTempWerdenNurMitFilterAusgelassen()
    {
        var temp = Path.Combine(Path.GetTempPath(), "x", "c_Pläne und Modelle", "e_KICAD");
        var d = Datei("A", E(temp, "Test", T(10)), E(P1, "Echt", T(5)));
        Assert.Equal(2, ZuletztListe.Vereinigen([d]).Count);
        Assert.Equal([P1], ZuletztListe.Vereinigen([d], tempFiltern: true).Select(z => z.Ordner));
    }

    [Fact]
    public void Vereinigen_LeerUndGleicheZeitOhneAbsturz()
    {
        Assert.Empty(ZuletztListe.Vereinigen([]));
        Assert.Empty(ZuletztListe.Vereinigen([Datei("A")]));
        var l = ZuletztListe.Vereinigen([Datei("A", E(P1, "Eins", T(10))), Datei("B", E(P1, "Zwei", T(10)))]);
        Assert.Single(l);
    }

    // ------------------------------------------------------------------ Dateien: je Rechner eine

    [Fact]
    public void JeRechnerEineDatei_KeinRechnerSchreibtDieDateiEinesAnderen()
    {
        var a = Rechner("PC-A");
        var b = Rechner("PC-B");
        a.Merken(P1, "Bernstrasse"); Weiter(5);
        b.Merken(P2, "Murtenstrasse"); Weiter(5);
        b.Anheften(P1, true);
        var dateien = Directory.EnumerateFiles(Path.Combine(wurzel, "zuletzt")).Select(Path.GetFileName).Order().ToList();
        Assert.Equal(["PC-A.json", "PC-B.json"], dateien);
        // Auf A steht nichts vom Anheften (das hat B vermerkt) – die Vereinigung zeigt es trotzdem
        var nurA = ZuletztListe.DateiLesen(Path.Combine(wurzel, "zuletzt", "PC-A.json"), "x")!;
        Assert.Single(nurA.Eintraege);
        Assert.Null(nurA.Eintraege[0].AngeheftetZeit);
        var ausSichtVonA = a.Alle();
        Assert.Equal(2, ausSichtVonA.Count);
        Assert.True(ausSichtVonA.Single(z => z.Ordner == P1).Angeheftet);
    }

    [Fact]
    public void AngeheftetAufDemEinenRechnerErscheintAufDemZweitenAngeheftet_UndLoesenAuchDort()
    {
        var a = Rechner("PC-A");
        var b = Rechner("PC-B");
        a.Merken(P1, "Bernstrasse"); Weiter(1);
        a.Anheften(P1, true); Weiter(1);
        Assert.True(b.Alle().Single().Angeheftet);
        Assert.Equal("PC-A", b.Alle().Single().Rechner);
        b.Anheften(P1, false);
        Assert.False(a.Alle().Single().Angeheftet);
        Assert.False(b.Alle().Single().Angeheftet);
    }

    [Fact]
    public void EntfernenAufDemEinenRechnerVerbirgtAufDemAnderen_ErneutesOeffnenZeigtWieder()
    {
        var a = Rechner("PC-A");
        var b = Rechner("PC-B");
        a.Merken(P1, "Bernstrasse"); a.Merken(P2, "Murtenstrasse"); Weiter(2);
        b.Entfernen(P1); Weiter(2);
        Assert.Equal([P2], a.Alle().Select(z => z.Ordner));
        a.Merken(P1, "Bernstrasse");
        Assert.Equal(2, b.Alle().Count);
    }

    [Fact]
    public void Entfernen_LoestAuchDieAnheftung()
    {
        var a = Rechner("PC-A");
        a.Merken(P1, "Bernstrasse"); Weiter(1);
        a.Anheften(P1, true); Weiter(1);
        a.Entfernen(P1); Weiter(1);
        Assert.Empty(a.Alle());
        a.Merken(P1, "Bernstrasse");
        var z = a.Alle().Single();
        Assert.False(z.Angeheftet);
    }

    [Fact]
    public void AnheftenOhneVorherGeoeffnetZuHaben_ZeigtDenEintragMitDemGegebenenNamen()
    {
        var a = Rechner("PC-A");
        a.Anheften(P3, true, "Ostring");
        var z = a.Alle().Single();
        Assert.Equal("Ostring", z.Name);
        Assert.True(z.Angeheftet);
    }

    [Fact]
    public void Kuerzen_BehaeltVermerkeUndDieNeuestenGeoeffneten()
    {
        var a = Rechner("PC-A");
        a.Anheften(P1, true, "Angeheftet"); Weiter(1);
        for (int i = 0; i < 80; i++) { a.Merken($@"W:\Objekte\P{i:D3}\c_Pläne und Modelle\e_KICAD", $"P{i}"); Weiter(1); }
        var d = ZuletztListe.DateiLesen(Path.Combine(wurzel, "zuletzt", "PC-A.json"), "x")!;
        Assert.True(d.Eintraege.Count <= 61);
        Assert.Contains(d.Eintraege, e => e.Ordner == P1);                                 // der Vermerk bleibt
        Assert.Contains(d.Eintraege, e => e.Ordner.Contains("P079"));                      // die neuesten bleiben
        Assert.DoesNotContain(d.Eintraege, e => e.Ordner.Contains("P000"));                // die ältesten gehen
    }

    [Fact]
    public void KonfliktkopienEinerDateiSchadenNicht()
    {
        var a = Rechner("PC-A");
        a.Merken(P1, "Bernstrasse");
        var ordner = Path.Combine(wurzel, "zuletzt");
        File.Copy(Path.Combine(ordner, "PC-A.json"), Path.Combine(ordner, "PC-A-DESKTOP-X1.json"));
        var l = a.Alle();
        Assert.Single(l);
    }

    [Fact]
    public void KaputteDateienWerdenUebergangen()
    {
        var a = Rechner("PC-A");
        a.Merken(P1, "Bernstrasse");
        File.WriteAllText(Path.Combine(wurzel, "zuletzt", "defekt.json"), "{ das ist kein json");
        File.WriteAllText(Path.Combine(wurzel, "zuletzt", "leer.json"), "");
        File.WriteAllText(Path.Combine(wurzel, "zuletzt", "falsch.json"), "42");
        Assert.Single(a.Alle());
    }

    [Fact]
    public void NichtVorhandenerOrdnerGibtLeereListe()
    {
        var a = Rechner("PC-A", ordner: Path.Combine(wurzel, "gibt-es-nicht"));
        Assert.Empty(a.Alle());
        Assert.False(Directory.Exists(Path.Combine(wurzel, "gibt-es-nicht")));   // Lesen legt nichts an
    }

    [Fact]
    public void RechnerNameMitUngueltigenZeichenWirdZumDateinamen()
    {
        var a = Rechner("HK/PC:1");
        a.Merken(P1, "Bernstrasse");
        Assert.Equal("HK_PC_1.json", Directory.EnumerateFiles(Path.Combine(wurzel, "zuletzt")).Select(Path.GetFileName).Single());
    }

    // ------------------------------------------------------------------ Übernahme der alten Liste

    string AlteListe(params (string Ordner, string Name, DateTimeOffset Zeit)[] e)
    {
        var pfad = Path.Combine(wurzel, "appdata", "zuletzt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, JsonSerializer.Serialize(e.Select(x => new { x.Ordner, x.Name, x.Zeit })));   // altes Format: Array, Schlüssel gross
        return pfad;
    }

    [Fact]
    public void AlteListe_WirdMitgelesen_NieGeschrieben_AuchNachEigenenSchreibvorgaengen()
    {
        var alt = AlteListe((P1, "Bernstrasse", T(10)), (P2, "Murtenstrasse", T(20)));
        var vorher = File.ReadAllText(alt);
        var a = Rechner("PC-A", alt);
        // Lesen allein schreibt nichts, zeigt aber die alten Einträge (als Einträge dieses Rechners)
        var l = a.Alle();
        Assert.Equal([P2, P1], l.Select(z => z.Ordner));
        Assert.All(l, z => Assert.Equal("PC-A", z.Rechner));
        Assert.False(Directory.Exists(Path.Combine(wurzel, "zuletzt")));
        // eigenes Schreiben: die neue Datei enthält nur Neues, die alte Liste bleibt dabei und bleibt unberührt
        Weiter(0);
        a.Merken(P3, "Ostring");
        var d = ZuletztListe.DateiLesen(Path.Combine(wurzel, "zuletzt", "PC-A.json"), "x")!;
        Assert.Single(d.Eintraege);
        Assert.Equal(3, a.Alle().Count);
        Assert.Equal(vorher, File.ReadAllText(alt));
        // ein Rechner ohne alte Liste sieht nur die neuen Dateien
        Assert.Single(Rechner("PC-B").Alle());
    }

    [Fact]
    public void AlteListe_NeuereVermerkeGewinnen_UndEinSpaeteresOeffnenZeigtWieder()
    {
        var alt = AlteListe((P1, "Bernstrasse", T(10)), (P2, "Murtenstrasse", T(20)));
        var a = Rechner("PC-A", alt);
        Weiter(60);                            // jetzt = 09:00, später als die alten Zeiten
        a.Entfernen(P1);
        a.Anheften(P2, true);
        var l = a.Alle();
        Assert.Equal([P2], l.Select(z => z.Ordner));
        Assert.True(l[0].Angeheftet);
        // ein älterer Server öffnet P1 später noch einmal (schreibt die alte Liste neu, mit späterer Zeit)
        File.WriteAllText(alt, JsonSerializer.Serialize(new[] { new { Ordner = P1, Name = "Bernstrasse", Zeit = jetzt.AddMinutes(30) } }));
        Assert.Equal(2, a.Alle().Count);
        Assert.False(a.Alle().Single(z => z.Ordner == P1).Angeheftet);
    }

    [Fact]
    public void AlteListe_KlemmtDasLesenKurz_GehenKeineEintraegeVerloren()
    {
        // Ein älterer Server schreibt die alte Datei nicht atomar: ein Lesen mitten im Schreiben sieht halbes JSON.
        var alt = AlteListe((P1, "Bernstrasse", T(10)));
        var vollstaendig = File.ReadAllText(alt);
        var a = Rechner("PC-A", alt);
        File.WriteAllText(alt, vollstaendig[..(vollstaendig.Length / 2)]);        // halb geschrieben
        Assert.Empty(a.Alle());                                                    // kein Absturz, nichts erfunden
        a.Merken(P3, "Ostring");                                                   // eigenes Schreiben verkürzt nichts
        File.WriteAllText(alt, vollstaendig);                                      // die Datei ist wieder ganz
        var l = a.Alle();
        Assert.Equal(2, l.Count);
        Assert.Contains(l, z => z.Ordner == P1);
        Assert.Contains(l, z => z.Ordner == P3);
    }

    [Fact]
    public void EinzeldateiOhneOneDrive_LiestUndSchreibtNurZuletztJson_AuchAusDemAltenFormat()
    {
        var ordner = Path.Combine(wurzel, "appdata");
        var alt = AlteListe((P1, "Bernstrasse", T(10)));
        var a = new ZuletztListe(ordner, "PC-A", false, null, false, "lokal") { Zwischenspeicher = TimeSpan.Zero, Uhr = () => jetzt };
        Assert.Single(a.Alle());
        a.Merken(P2, "Murtenstrasse");
        Assert.Equal(["zuletzt.json"], Directory.EnumerateFiles(ordner).Select(Path.GetFileName).ToList());
        Assert.Equal(2, a.Alle().Count);
        Assert.Contains("\"eintraege\"", File.ReadAllText(alt));   // jetzt im neuen Format
    }

    // ------------------------------------------------------------------ Schutz

    [Fact]
    public void Testlauf_BeschreibtNieEinenOrdnerAusserhalbVonTemp()
    {
        var aussen = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_build", "s1", "zuletzt_sperre_" + Guid.NewGuid().ToString("N")[..6]);
        Assert.False(aussen.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase));
        var a = new ZuletztListe(aussen, "PC-A", true, null, false, "Test");
        var f = Assert.Throws<HlksFehler>(() => a.Anheften(P1, true));
        Assert.Contains("Testlauf", f.Message);
        a.Merken(P1, "Bernstrasse");   // Komfortfunktion: still, aber ebenfalls ohne Schreiben
        Assert.False(Directory.Exists(aussen));
    }

    [Fact]
    public void Anheften_OhneOrdnerIstEinFehlerMitVorschlag()
    {
        var f = Assert.Throws<HlksFehler>(() => Rechner("PC-A").Anheften(" ", true));
        Assert.False(string.IsNullOrEmpty(f.Vorschlag));
    }

    [Fact]
    public void Aus_NimmtDieUmleitungVorOneDriveUndAppdata()
    {
        var e = new Einstellungen("T", "PC-X", Katalog.StandardOrdner(), null) { ZuletztOrdner = Path.Combine(wurzel, "umgeleitet") };
        var z = ZuletztListe.Aus(e);
        Assert.Equal("Umleitung", z.Quelle);
        Assert.Equal(Path.Combine(wurzel, "umgeleitet"), z.Verzeichnis);
        Assert.Equal("PC-X", z.Rechner);
        z.Merken(P1, "Bernstrasse");    // Umleitung unter %TEMP%: im Test erlaubt, und Testprojekt-Filter aus
        Assert.True(File.Exists(Path.Combine(wurzel, "umgeleitet", "PC-X.json")));
    }

    [Fact]
    public void TempFilter_MerktKeineTestprojekteInDerEchtenListe()
    {
        var a = new ZuletztListe(Path.Combine(wurzel, "zuletzt"), "PC-A", true, null, true, "Test") { Zwischenspeicher = TimeSpan.Zero, Uhr = () => jetzt };
        a.Merken(Path.Combine(Path.GetTempPath(), "versuch", "e_KICAD"), "Versuch");
        Assert.Empty(a.Alle());
        Assert.False(File.Exists(Path.Combine(wurzel, "zuletzt", "PC-A.json")));
    }
}
