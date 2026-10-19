// Tests der Projektablage (OrdnerSpeicher) und der Zusammenarbeit mehrerer Bearbeiter über dieselbe Ablage.
// Jeder Test arbeitet in einem eigenen Temp-Ordner, dessen Pfad wie auf dem NAS «ä» und Leerzeichen enthält.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;

namespace Hlks.Kern.Tests;

public sealed class SpeicherTests : IDisposable
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());

    readonly ITestOutputHelper aus;
    readonly string wurzel;
    /// <summary>…\Projekt Müller\c_Pläne und Modelle\e_KICAD – wie auf dem NAS.</summary>
    readonly string ordner;

    public SpeicherTests(ITestOutputHelper aus)
    {
        this.aus = aus;
        wurzel = Path.Combine(Path.GetTempPath(), "hlks-test-" + Guid.NewGuid());
        ordner = Path.Combine(wurzel, "Projekt Müller", "c_Pläne und Modelle", "e_KICAD");
        new OrdnerSpeicher(ordner).Anlegen();   // Ablage samt Unterordnern; der Konstruktor legt nichts an
    }

    public void Dispose()
    {
        for (int i = 0; i < 3 && Directory.Exists(wurzel); i++)
        {
            try { Directory.Delete(wurzel, recursive: true); }
            catch (IOException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { Thread.Sleep(100); }
        }
    }

    // ---------------------------------------------------------------- Hilfen

    string Unterordner(string name) => Path.Combine(ordner, name);

    Projekt NeuesProjekt(string akteur, string geraet, TimeSpan? dauer = null) =>
        Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner, dauer), "Testprojekt Müller", akteur, geraet);

    Projekt Oeffnen(string akteur, string geraet, TimeSpan? dauer = null) =>
        Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner, dauer), akteur, geraet);

    static BefehlsErgebnis Ok(BefehlsErgebnis e)
    {
        Assert.True(e.Angenommen, $"{e.Befehl}: {e.Meldung} {e.Fehler} {e.Vorschlag}");
        return e;
    }

    static BefehlsErgebnis GebaeudeAnlegen(Projekt p, string kz) =>
        p.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = kz });

    static BefehlsErgebnis Umbenennen(Projekt p, string kz, string name) =>
        p.Ausfuehren("element_aendern", new JsonObject { ["element"] = kz, ["name"] = name });

    static Paket TestPaket(string id, string akteur = "A", string befehl = "test") => new()
    {
        Id = id, Akteur = akteur, Geraet = "pc-a", Zeit = new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.FromHours(2)), Befehl = befehl,
        Parameter = new JsonObject { ["hinweis"] = "Prüfung äöü «Zitat»" },
    };

    static string Inhalt(Modell m) => Json.Text(m.Abbild([]).Elemente) + Json.Text(m.Abbild([]).Verbindungen);

    // ---------------------------------------------------------------- 1. Beispiel speichern und öffnen

    [Fact]
    public void BeispielSpeichernUndOeffnen()
    {
        var original = Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(ordner));
        Assert.Equal(175, original.Modell.Elemente.Count);
        Assert.Equal(138, original.Modell.Verbindungen.Count);

        var speicher = new OrdnerSpeicher(ordner);
        Assert.Equal(original.Verlauf.Count, speicher.PaketListe().Count);
        aus.WriteLine($"Pakete: {speicher.PaketListe().Count}, Standdateien: {Directory.GetFiles(Unterordner("stand")).Length}");
        Assert.NotNull(speicher.StandLesen());

        // mit Standabbild
        var b = Projekt.Oeffnen(Kat, speicher, "B", "pc-b");
        Assert.Equal(original.Modell.Pruefsumme(), b.Modell.Pruefsumme());
        Assert.Equal(175, b.Modell.Elemente.Count);
        Assert.Equal(138, b.Modell.Verbindungen.Count);
        Assert.Equal(original.Verlauf.Count, b.BekanntePakete.Count);
        Assert.Equal(Inhalt(original.Modell), Inhalt(b.Modell));

        // nur aus Paketen
        Directory.Delete(Unterordner("stand"), recursive: true);
        var c = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "C", "pc-c");
        Assert.Equal(original.Modell.Pruefsumme(), c.Modell.Pruefsumme());
        Assert.Equal(175, c.Modell.Elemente.Count);
        Assert.Equal(138, c.Modell.Verbindungen.Count);
        Assert.Equal(original.Verlauf.Count, c.Verlauf.Count);
        Assert.Equal(Inhalt(original.Modell), Inhalt(c.Modell));
    }

    [Fact]
    public void OeffnenMitStandUndNeuerenPaketen()
    {
        var a = NeuesProjekt("A", "pc-a");
        foreach (var kz in new[] { "GEB-1", "GEB-2", "GEB-3" }) Ok(GebaeudeAnlegen(a, kz));
        a.StandSichern();
        Ok(GebaeudeAnlegen(a, "GEB-4"));
        Ok(Umbenennen(a, "GEB-1", "nach dem Stand geändert"));

        var b = Oeffnen("B", "pc-b");
        Assert.Equal(4, b.Modell.Elemente.Count);
        Assert.Equal(5, b.BekanntePakete.Count);
        Assert.Equal(2, b.Verlauf.Count);                         // nur die zwei Pakete nach dem Stand angewandt
        Assert.Equal("nach dem Stand geändert", b.Modell.Finde("GEB-1")!.Name);
        Assert.Equal(a.Modell.Pruefsumme(), b.Modell.Pruefsumme());
    }

    // ---------------------------------------------------------------- 2. Pakete nie überschreiben

    [Fact]
    public void PaketWirdNieUeberschrieben()
    {
        var s = new OrdnerSpeicher(ordner);
        const string id = "20261017T100000000Z_pc-a_A_000001";
        s.PaketSchreiben(TestPaket(id, "A", "erster"));
        var pfad = Path.Combine(Unterordner("aenderungen"), id + ".json");
        var vorher = File.ReadAllBytes(pfad);

        var ex = Assert.ThrowsAny<IOException>(() => s.PaketSchreiben(TestPaket(id, "B", "zweiter")));
        aus.WriteLine(ex.Message);

        Assert.Equal(vorher, File.ReadAllBytes(pfad));
        Assert.Equal("erster", s.PaketLesen(id).Befehl);
        Assert.Equal(new[] { id }, s.PaketListe());
        Assert.Empty(Directory.GetFiles(Unterordner("aenderungen"), ".*"));   // keine Temp-Datei liegen geblieben
    }

    [Fact]
    public void ProjektWeichtBeiBesetzterPaketIdAufNeueNummerAus()
    {
        // Projekt.Uebernehmen verlässt sich darauf, dass PaketSchreiben bei besetzter Id IOException wirft.
        var a = NeuesProjekt("A", "pc-a");
        var e = Ok(GebaeudeAnlegen(a, "GEB-1"));
        Assert.Equal(new[] { e.PaketId! }, a.Speicher!.PaketListe());
        Assert.ThrowsAny<IOException>(() => a.Speicher.PaketSchreiben(a.Speicher.PaketLesen(e.PaketId!)));
        Ok(GebaeudeAnlegen(a, "GEB-2"));
        Assert.Equal(2, a.Speicher.PaketListe().Count);
    }

    [Fact]
    public void PaketRundreiseUtf8OhneBom()
    {
        var s = new OrdnerSpeicher(ordner);
        const string id = "20261017T100000000Z_pc-a_A_000001";
        var p = TestPaket(id);
        p.Operationen.Add(new Operation
        {
            Art = "element.setzen", Id = "e1", Basis = 0,
            Element = new Element { Id = "e1", Kennzeichen = "WC-Ü1", Klasse = "WC", Name = "Waschküche «Süd»", Version = 1 },
        });
        s.PaketSchreiben(p);

        var bytes = File.ReadAllBytes(Path.Combine(Unterordner("aenderungen"), id + ".json"));
        Assert.False(bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }), "Datei beginnt mit BOM");
        var text = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        Assert.Contains("Waschküche «Süd»", text);              // Umlaute und Guillemets lesbar, nicht als \u-Folge
        Assert.Contains("\"befehl\"", text);                       // camelCase

        var gelesen = s.PaketLesen(id);
        Assert.Equal(Json.Text(p), Json.Text(gelesen));
    }

    [Fact]
    public void LesenUebergehtBomAusFremdenWerkzeugen()
    {
        var s = new OrdnerSpeicher(ordner);
        const string id = "20261017T100000000Z_pc-a_A_000001";
        File.WriteAllText(Path.Combine(Unterordner("aenderungen"), id + ".json"), Json.Text(TestPaket(id), true), new UTF8Encoding(true));
        Assert.Equal("Prüfung äöü «Zitat»", s.PaketLesen(id).Parameter!["hinweis"]!.GetValue<string>());
    }

    [Fact]
    public void UngueltigePaketIdWirdAbgewiesen()
    {
        var s = new OrdnerSpeicher(ordner);
        foreach (var id in new[] { "", " ", ".versteckt", "a/b", "..\\x", "a:b", "a*b" })
            Assert.ThrowsAny<ArgumentException>(() => s.PaketSchreiben(TestPaket(id)));
        Assert.Empty(s.PaketListe());
    }

    // ---------------------------------------------------------------- 3. Zwei Bearbeiter

    [Fact]
    public void ZweiBearbeiterSehenFremdeAenderungen()
    {
        var a = NeuesProjekt("A", "pc-a");
        var b = Oeffnen("B", "pc-b");
        var ereignisse = new List<ModellAenderung>();
        b.Geaendert += ereignisse.Add;

        var e = Ok(GebaeudeAnlegen(a, "GEB-T"));
        var sync = b.Synchronisieren();

        Assert.Equal(1, sync.NeuePakete);
        Assert.Empty(sync.Konflikte);
        var el = b.Modell.Finde("GEB-T");
        Assert.NotNull(el);
        var ereignis = Assert.Single(ereignisse);
        Assert.Equal("fremd", ereignis.Quelle);
        Assert.Equal("A", ereignis.Akteur);
        Assert.Equal(e.PaketId, ereignis.PaketId);
        Assert.Contains(el.Id, ereignis.ElementIds);
        Assert.Equal(a.Modell.Pruefsumme(), b.Modell.Pruefsumme());

        Assert.Equal(0, b.Synchronisieren().NeuePakete);           // nichts doppelt
        Assert.Single(ereignisse);
    }

    [Fact]
    public async Task ZweiBearbeiterSchreibenGleichzeitig()
    {
        var a = NeuesProjekt("A", "pc-a");
        var b = Oeffnen("B", "pc-b");
        const int n = 15;
        using var start = new Barrier(2);
        Task Bearbeiter(Projekt p, string praefix) => Task.Factory.StartNew(() =>
        {
            start.SignalAndWait();
            for (int i = 0; i < n; i++) Ok(GebaeudeAnlegen(p, $"{praefix}{i}"));
        }, TaskCreationOptions.LongRunning);
        await Task.WhenAll(Bearbeiter(a, "GEB-A"), Bearbeiter(b, "GEB-B"));

        Assert.Equal(2 * n, a.Speicher!.PaketListe().Count);
        Assert.Equal(n, a.Synchronisieren().NeuePakete);
        Assert.Equal(n, b.Synchronisieren().NeuePakete);
        Assert.Equal(2 * n, a.Modell.Elemente.Count);
        Assert.Equal(a.Modell.Pruefsumme(), b.Modell.Pruefsumme());
        Assert.Equal(a.Modell.Pruefsumme(), Oeffnen("C", "pc-c").Modell.Pruefsumme());
    }

    // ---------------------------------------------------------------- 4. Reservierung blockiert

    [Fact]
    public void ReservierungBlockiertFremdeAenderung()
    {
        var a = NeuesProjekt("A", "pc-a");
        Ok(GebaeudeAnlegen(a, "GEB-X"));
        Assert.Empty(a.Speicher!.Reservierungen());              // Anlegen reserviert nichts
        var b = Oeffnen("B", "pc-b");

        Ok(Umbenennen(a, "GEB-X", "von A"));
        var id = a.Modell.Finde("GEB-X")!.Id;
        var r = Assert.Single(a.Speicher.Reservierungen());
        Assert.Equal((id, "A", "pc-a"), (r.Element, r.Akteur, r.Geraet));
        Assert.True(r.Bis > DateTimeOffset.Now.AddMinutes(100));  // Vorgabe 2 h

        b.Synchronisieren();
        var abgelehnt = Umbenennen(b, "GEB-X", "von B");
        Assert.False(abgelehnt.Angenommen);
        var g = Assert.Single(abgelehnt.Gesperrt);
        Assert.Equal(("A", "pc-a"), (g.Akteur, g.Geraet));
        Assert.Contains("GEB-X", abgelehnt.Fehler);
        Assert.Equal("von A", b.Modell.Finde("GEB-X")!.Name);   // nichts geändert
        Assert.Equal(2, b.Speicher!.PaketListe().Count);         // kein Paket geschrieben

        a.Freigeben();
        Assert.Empty(a.Speicher.Reservierungen());
        Ok(Umbenennen(b, "GEB-X", "von B"));
        a.Synchronisieren();
        Assert.Equal("von B", a.Modell.Finde("GEB-X")!.Name);
    }

    // ---------------------------------------------------------------- 5. Abgelaufene Reservierung

    [Fact]
    public void AbgelaufeneReservierungGiltNicht()
    {
        var a = NeuesProjekt("A", "pc-a", TimeSpan.FromMilliseconds(50));
        Ok(GebaeudeAnlegen(a, "GEB-X"));
        Ok(Umbenennen(a, "GEB-X", "von A"));
        Assert.Single(Directory.GetFiles(Unterordner("sperren"), "*.json"));
        Thread.Sleep(200);

        var b = Oeffnen("B", "pc-b");
        Assert.Empty(b.Speicher!.Reservierungen());
        Ok(Umbenennen(b, "GEB-X", "von B"));
        var r = Assert.Single(b.Speicher.Reservierungen());
        Assert.Equal(("B", "pc-b"), (r.Akteur, r.Geraet));
    }

    // ---------------------------------------------------------------- 6. Konflikt bei veraltetem Stand

    [Fact]
    public void KonfliktBeiVeraltetemStand()
    {
        var a = NeuesProjekt("A", "pc-a");
        Ok(GebaeudeAnlegen(a, "GEB-X"));
        var b = Oeffnen("B", "pc-b");
        var id = a.Modell.Finde("GEB-X")!.Id;

        Ok(Umbenennen(a, "GEB-X", "von A"));
        a.Freigeben();
        var eb = Ok(Umbenennen(b, "GEB-X", "von B"));            // B hat nicht synchronisiert
        Assert.Empty(eb.Konflikte);

        var sync = a.Synchronisieren();
        Assert.Equal(1, sync.NeuePakete);
        var k = Assert.Single(sync.Konflikte);
        Assert.Equal((id, 1, 2), (k.Id, k.Erwartet, k.Vorhanden));
        Assert.Equal("von B", a.Modell.Finde("GEB-X")!.Name);    // neuere Änderung von B
        Assert.Equal(3, a.Modell.Finde("GEB-X")!.Version);
    }

    [Fact]
    public void KonfliktLaeuftAufAllenGeraetenGleichAus()
    {
        var a = NeuesProjekt("A", "pc-a");
        Ok(GebaeudeAnlegen(a, "GEB-X"));
        var b = Oeffnen("B", "pc-b");
        Ok(Umbenennen(a, "GEB-X", "von A"));
        a.Freigeben();
        Ok(Umbenennen(b, "GEB-X", "von B"));
        a.Synchronisieren();
        b.Synchronisieren();
        var c = Oeffnen("C", "pc-c");
        Assert.Equal("von B", a.Modell.Finde("GEB-X")!.Name);
        Assert.Equal("von B", c.Modell.Finde("GEB-X")!.Name);
        Assert.Equal("von B", b.Modell.Finde("GEB-X")!.Name);
    }

    // ---------------------------------------------------------------- 7. Rückgängig über Geräte

    [Fact]
    public void RueckgaengigUeberGeraete()
    {
        var a = NeuesProjekt("A", "pc-a");
        var b = Oeffnen("B", "pc-b");
        Ok(GebaeudeAnlegen(a, "GEB-T"));
        b.Synchronisieren();
        Assert.NotNull(b.Modell.Finde("GEB-T"));

        var r = Ok(a.Rueckgaengig());
        var sync = b.Synchronisieren();
        Assert.Equal(1, sync.NeuePakete);
        Assert.Empty(sync.Konflikte);
        Assert.Null(b.Modell.Finde("GEB-T"));
        Assert.Equal(2, a.Speicher!.PaketListe().Count);         // Rückgängig ist ein weiteres Paket
        Assert.Equal(r.PaketId, b.Verlauf[^1].Id);
        Assert.NotNull(b.Verlauf[^1].RueckgaengigVon);
        Assert.Equal(a.Modell.Pruefsumme(), b.Modell.Pruefsumme());
    }

    // ---------------------------------------------------------------- 8. Temp-Dateien

    [Fact]
    public void TempDateienWerdenIgnoriert()
    {
        var s = new OrdnerSpeicher(ordner);
        s.KopfSchreiben(new ProjektKopf { Id = "p1", Name = "Test", Angelegt = DateTimeOffset.Now });
        const string id = "20261017T100000000Z_pc-a_A_000001";
        s.PaketSchreiben(TestPaket(id));
        var aenderungen = Unterordner("aenderungen");
        File.WriteAllText(Path.Combine(aenderungen, ".irgendwas.tmp"), "halb geschrie");
        File.WriteAllText(Path.Combine(aenderungen, ".20261017T100000000Z_pc-b_B_000001.ab12cd34.tmp"), "{");
        File.WriteAllText(Path.Combine(aenderungen, ".versteckt.json"), "{}");
        File.WriteAllText(Path.Combine(aenderungen, "notiz.txt"), "keine Paketdatei");

        Assert.Equal(new[] { id }, s.PaketListe());
        var p = Oeffnen("B", "pc-b");                              // Öffnen stört sich nicht daran
        Assert.Single(p.BekanntePakete);
    }

    // ---------------------------------------------------------------- 9. Ablage nicht erreichbar

    [Fact]
    public void AblageWegMeldetNichtErreichbar()
    {
        var a = NeuesProjekt("A", "pc-a");
        Directory.Delete(ordner, recursive: true);

        var e = GebaeudeAnlegen(a, "GEB-T");
        Assert.False(e.Angenommen);
        Assert.Contains("nicht erreichbar", e.Meldung);
        Assert.Null(a.Modell.Finde("GEB-T"));
        Assert.False(Directory.Exists(ordner));                  // die Ablage wird nicht stillschweigend neu angelegt
    }

    [Fact]
    public void AblageWegBeiAenderungMeldetNichtErreichbar()
    {
        var a = NeuesProjekt("A", "pc-a");
        Ok(GebaeudeAnlegen(a, "GEB-T"));
        Directory.Delete(ordner, recursive: true);

        var e = Umbenennen(a, "GEB-T", "neu");
        Assert.False(e.Angenommen);
        Assert.Contains("nicht erreichbar", e.Meldung);
    }

    [Fact]
    public void SpeicherReichtZugriffsfehlerDurch()
    {
        var s = new OrdnerSpeicher(ordner);
        Directory.Delete(ordner, recursive: true);
        var id = Guid.NewGuid().ToString();

        Assert.ThrowsAny<IOException>(() => s.KopfLesen());
        Assert.ThrowsAny<IOException>(() => s.KopfSchreiben(new ProjektKopf { Id = "x", Name = "x" }));
        Assert.ThrowsAny<IOException>(() => s.StandLesen());
        Assert.ThrowsAny<IOException>(() => s.StandSchreiben(new StandAbbild()));
        Assert.ThrowsAny<IOException>(() => s.PaketListe());
        Assert.ThrowsAny<IOException>(() => s.PaketLesen("20261017T100000000Z_pc-a_A_000001"));
        Assert.ThrowsAny<IOException>(() => s.PaketSchreiben(TestPaket("20261017T100000000Z_pc-a_A_000001")));
        Assert.ThrowsAny<IOException>(() => s.Reservieren([id], "A", "pc-a"));
        Assert.ThrowsAny<IOException>(() => s.Reservierungen());
        Assert.ThrowsAny<IOException>(() => s.Freigeben("A", "pc-a"));
        Assert.Empty(s.Reservieren([], "A", "pc-a"));             // nichts zu reservieren → kein Zugriff nötig
        Assert.False(Directory.Exists(ordner));
    }

    // ---------------------------------------------------------------- 10. Öffnen ist schnell

    [Fact]
    public void OeffnenMitStandabbildIstSchnell()
    {
        var original = Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(ordner));
        var uhr = Stopwatch.StartNew();
        var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "B", "pc-b");
        uhr.Stop();
        aus.WriteLine($"Öffnen mit Standabbild: {uhr.ElapsedMilliseconds} ms");
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(3), $"Öffnen dauerte {uhr.Elapsed.TotalSeconds:F2} s");
        Assert.Equal(original.Modell.Pruefsumme(), b.Modell.Pruefsumme());
        Assert.Empty(b.Verlauf);                                   // alles kam aus dem Standabbild
    }

    // ---------------------------------------------------------------- Kopf

    [Fact]
    public void KopfSchreibenUndLesen()
    {
        // Der Konstruktor legt nichts an (vertippter Pfad, NAS getrennt); erst Anlegen() erzeugt Ordner und Unterordner.
        var frisch = Path.Combine(wurzel, "Projekt Neu", "c_Pläne und Modelle", "e_KICAD");
        var f = new OrdnerSpeicher(frisch);
        Assert.False(Directory.Exists(frisch));
        f.Anlegen();
        foreach (var u in new[] { "stand", "aenderungen", "sperren", "unterlagen" }) Assert.True(Directory.Exists(Path.Combine(frisch, u)), u);

        var s = new OrdnerSpeicher(ordner);
        Assert.Equal(ordner, s.Ort);
        Assert.False(File.Exists(Path.Combine(ordner, "projekt.json")));
        Assert.Null(s.KopfLesen());

        var kopf = new ProjektKopf { Id = "p1", Name = "MFH Bärenweg", Angelegt = DateTimeOffset.Now, Notiz = "Schützenmatt" };
        s.KopfSchreiben(kopf);
        Assert.Equal(Json.Text(kopf), Json.Text(s.KopfLesen()!));
        kopf.Name = "MFH Bärenweg, 2. Etappe";
        s.KopfSchreiben(kopf);                                     // ersetzt atomar
        Assert.Equal("MFH Bärenweg, 2. Etappe", s.KopfLesen()!.Name);
        Assert.Empty(Directory.GetFiles(ordner, ".*"));            // keine Temp-Datei übrig

        Assert.Throws<HlksFehler>(() => Projekt.Anlegen(Kat, s, "zweites", "A", "pc-a"));
    }

    // ---------------------------------------------------------------- Standabbilder

    static StandAbbild Stand(string marke) => new() { Zeit = DateTimeOffset.Now, EnthaltenePakete = [marke] };

    [Fact]
    public void StandBehaeltNurDieDreiNeuesten()
    {
        var s = new OrdnerSpeicher(ordner);
        Assert.Null(s.StandLesen());
        for (int i = 1; i <= 5; i++) s.StandSchreiben(Stand(i.ToString()));   // teils in derselben Millisekunde

        var dateien = Directory.GetFiles(Unterordner("stand"));
        Assert.Equal(3, dateien.Length);
        Assert.All(dateien, d => Assert.Matches(@"^stand-\d{8}T\d{9}Z-[a-z0-9]{4}\.json$", Path.GetFileName(d)));
        Assert.Equal(new[] { "5" }, s.StandLesen()!.EnthaltenePakete);
    }

    [Fact]
    public void StandLesenNimmtNaechstaelteresWennNeuestesKaputt()
    {
        var s = new OrdnerSpeicher(ordner);
        s.StandSchreiben(Stand("alt"));
        s.StandSchreiben(Stand("neu"));
        var neueste = Directory.GetFiles(Unterordner("stand")).Order(StringComparer.Ordinal).Last();
        File.WriteAllText(neueste, "{ \"elemente\": [ abgebrochen");
        Assert.Equal(new[] { "alt" }, s.StandLesen()!.EnthaltenePakete);

        foreach (var d in Directory.GetFiles(Unterordner("stand"))) File.WriteAllText(d, "");
        Assert.Null(s.StandLesen());

        Directory.Delete(Unterordner("stand"), recursive: true);
        Assert.Null(s.StandLesen());                               // fehlender Ordner: kein Stand
        s.StandSchreiben(Stand("wieder"));                         // abgeleitete Daten: Ordner wird neu angelegt
        Assert.Equal(new[] { "wieder" }, s.StandLesen()!.EnthaltenePakete);
    }

    // ---------------------------------------------------------------- Pakete lesen

    [Fact]
    public async Task PaketLesenWartetAufGesperrteDatei()
    {
        var s = new OrdnerSpeicher(ordner);
        const string id = "20261017T100000000Z_pc-a_A_000001";
        s.PaketSchreiben(TestPaket(id));
        var pfad = Path.Combine(Unterordner("aenderungen"), id + ".json");

        var sperre = new FileStream(pfad, FileMode.Open, FileAccess.ReadWrite, FileShare.None);   // «wird gerade geschrieben»
        var uhr = Stopwatch.StartNew();
        var freigabe = Task.Run(async () => { await Task.Delay(150); sperre.Dispose(); });
        Assert.Equal(id, s.PaketLesen(id).Id);
        aus.WriteLine($"gelesen nach {uhr.ElapsedMilliseconds} ms");
        Assert.True(uhr.ElapsedMilliseconds >= 100, "Lesen hätte auf die Freigabe warten müssen");
        await freigabe;
    }

    [Fact]
    public void PaketLesenMeldetDauerhaftGesperrteOderKaputteDatei()
    {
        var s = new OrdnerSpeicher(ordner);
        const string id = "20261017T100000000Z_pc-a_A_000001";
        s.PaketSchreiben(TestPaket(id));
        var pfad = Path.Combine(Unterordner("aenderungen"), id + ".json");
        using (new FileStream(pfad, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.ThrowsAny<IOException>(() => s.PaketLesen(id));

        File.WriteAllText(pfad, "{ \"id\": ");
        var ex = Assert.ThrowsAny<IOException>(() => s.PaketLesen(id));
        Assert.Contains(id, ex.Message);
        Assert.Throws<FileNotFoundException>(() => s.PaketLesen("20261017T100000000Z_pc-a_A_999999"));
    }

    // ---------------------------------------------------------------- Reservierungen direkt

    [Fact]
    public void ReservierenIstAllesOderNichts()
    {
        var s = new OrdnerSpeicher(ordner);
        string id1 = Guid.NewGuid().ToString(), id2 = Guid.NewGuid().ToString(), id3 = Guid.NewGuid().ToString();
        Assert.Empty(s.Reservieren([id2], "B", "pc-b"));

        var konflikte = s.Reservieren([id1, id2, id3], "A", "pc-a");
        var k = Assert.Single(konflikte);
        Assert.Equal((id2, "B", "pc-b"), (k.Element, k.Akteur, k.Geraet));
        var alle = s.Reservierungen();
        Assert.Equal(new[] { id2 }, alle.Select(r => r.Element));          // id1 und id3 nicht reserviert
        Assert.Single(Directory.GetFiles(Unterordner("sperren")));
    }

    [Fact]
    public void GleicherAkteurAnderesGeraetIstFremd()
    {
        var s = new OrdnerSpeicher(ordner);
        var id = Guid.NewGuid().ToString();
        Assert.Empty(s.Reservieren([id], "A", "pc-a"));
        Assert.Single(s.Reservieren([id], "A", "notebook-a"));
        Assert.Single(s.Reservieren([id], "B", "pc-a"));
        Assert.Empty(s.Reservieren([id], "A", "pc-a"));
    }

    [Fact]
    public void EigeneReservierungWirdVerlaengert()
    {
        var s = new OrdnerSpeicher(ordner, TimeSpan.FromMinutes(10));
        var id = Guid.NewGuid().ToString();
        s.Reservieren([id], "A", "pc-a");
        var erste = Assert.Single(s.Reservierungen());
        Thread.Sleep(30);
        Assert.Empty(s.Reservieren([id], "A", "pc-a"));
        var zweite = Assert.Single(s.Reservierungen());
        Assert.Equal(erste.Seit, zweite.Seit);
        Assert.True(zweite.Bis > erste.Bis);
        Assert.Equal(TimeSpan.FromMinutes(10), erste.Bis - erste.Seit);
    }

    [Fact]
    public void FreigebenNurEigene()
    {
        var s = new OrdnerSpeicher(ordner);
        string id1 = Guid.NewGuid().ToString(), id2 = Guid.NewGuid().ToString(), id3 = Guid.NewGuid().ToString();
        s.Reservieren([id1, id2], "A", "pc-a");
        s.Reservieren([id3], "B", "pc-b");

        s.Freigeben("A", "pc-a", [id1, id3]);                     // id3 gehört B → bleibt
        Assert.Equal(new[] { id2, id3 }.Order(StringComparer.Ordinal), s.Reservierungen().Select(r => r.Element));
        s.Freigeben("A", "pc-b");                                  // falsches Gerät → nichts
        Assert.Equal(2, s.Reservierungen().Count);
        s.Freigeben("A", "pc-a");
        Assert.Equal(new[] { id3 }, s.Reservierungen().Select(r => r.Element));
        s.Freigeben("A", "pc-a", [Guid.NewGuid().ToString()]);    // gibt es nicht → kein Fehler
    }

    [Fact]
    public void AbgelaufeneUndVerwaisteReservierungenWerdenErsetzt()
    {
        var kurz = new OrdnerSpeicher(ordner, TimeSpan.FromMilliseconds(30));
        var s = new OrdnerSpeicher(ordner);
        string id1 = Guid.NewGuid().ToString(), id2 = Guid.NewGuid().ToString();
        kurz.Reservieren([id1], "A", "pc-a");
        File.WriteAllText(Path.Combine(Unterordner("sperren"), id2 + ".json"), "");   // abgebrochen geschrieben
        Thread.Sleep(100);
        Assert.Empty(s.Reservierungen());

        Assert.Empty(s.Reservieren([id1, id2], "B", "pc-b"));
        Assert.Equal(new[] { id1, id2 }.Order(StringComparer.Ordinal), s.Reservierungen().Select(r => r.Element));
        Assert.All(s.Reservierungen(), r => Assert.Equal("B", r.Akteur));
    }

    [Fact]
    public void SperrDateinamenSindSicherUndEindeutig()
    {
        var s = new OrdnerSpeicher(ordner);
        var ids = new[] { "a/b\\c:d*e?f\"g<h>i|j", "ABC", "abc", "con", "CON", "..", ".x", "Ä ü", new string('z', 300), new string('z', 299) + "Z" };
        Assert.Empty(s.Reservieren(ids, "A", "pc-a"));

        var dateien = Directory.GetFiles(Unterordner("sperren"));
        Assert.Equal(ids.Length, dateien.Length);
        Assert.All(dateien, d => Assert.Equal(Unterordner("sperren"), Path.GetDirectoryName(d)));
        Assert.Equal(ids.Order(StringComparer.Ordinal), s.Reservierungen().Select(r => r.Element));
        Assert.Single(s.Reservieren(["abc"], "B", "pc-b"));        // «abc» und «ABC» sind getrennt reserviert
        s.Freigeben("A", "pc-a", ids);
        Assert.Empty(s.Reservierungen());
        Assert.Empty(Directory.GetFiles(Unterordner("sperren")));
    }

    [Fact]
    public async Task ZwischenzeitlichAngelegteFremdeReservierungFuehrtZurRuecknahme()
    {
        // Ablauf: Die Sperrdatei von «strittig» ist bei der Prüfung unlesbar (verwaist) → kein Konflikt. B legt «eigen» an;
        // beim Anlegen von «strittig» liegt dort inzwischen eine gültige Reservierung von A → B nimmt «eigen» zurück.
        // Je nach Takt erkennt B den Konflikt schon bei der Prüfung; das Ergebnis muss in beiden Fällen dasselbe sein.
        var s = new OrdnerSpeicher(ordner);
        const string eigen = "00000000-0000-0000-0000-000000000001", strittig = "ffffffff-0000-0000-0000-000000000002";
        var pfad = Path.Combine(Unterordner("sperren"), strittig + ".json");
        File.WriteAllText(pfad, "");
        var jetzt = DateTimeOffset.Now;
        var vonA = Task.Run(async () =>
        {
            await Task.Delay(850);                                  // Prüfung von B (≈ 750 ms Wiederholungen) ist dann vorbei,
                                                                    // das erneute Lesen nach CreateNew (bis ≈ 1500 ms) noch nicht
            var tmp = Path.Combine(Unterordner("sperren"), ".a.tmp");
            File.WriteAllText(tmp, Json.Text(new Reservierung(strittig, "A", "pc-a", jetzt, jetzt.AddHours(2)), true));
            File.Move(tmp, pfad, overwrite: true);
        });

        var konflikte = s.Reservieren([eigen, strittig], "B", "pc-b");
        await vonA;

        var k = Assert.Single(konflikte);
        Assert.Equal((strittig, "A"), (k.Element, k.Akteur));
        var r = Assert.Single(s.Reservierungen());
        Assert.Equal((strittig, "A"), (r.Element, r.Akteur));
        Assert.False(File.Exists(Path.Combine(Unterordner("sperren"), eigen + ".json")));
    }

    [Fact]
    public async Task WettlaufUmDieselbeReservierungHatGenauEinenGewinner()
    {
        const int teilnehmer = 8, runden = 5;
        for (int runde = 0; runde < runden; runde++)
        {
            var gemeinsam = Guid.NewGuid().ToString();
            var eigene = Enumerable.Range(0, teilnehmer).Select(_ => Guid.NewGuid().ToString()).ToArray();
            var ergebnis = new IReadOnlyList<Reservierung>[teilnehmer];
            using var start = new Barrier(teilnehmer);
            // je Teilnehmer ein eigener Thread (LongRunning), damit alle gleichzeitig an der Barriere stehen
            await Task.WhenAll(Enumerable.Range(0, teilnehmer).Select(i => Task.Factory.StartNew(() =>
            {
                var s = new OrdnerSpeicher(ordner);
                start.SignalAndWait();
                ergebnis[i] = s.Reservieren([eigene[i], gemeinsam], $"P{i}", $"pc-{i}");
            }, TaskCreationOptions.LongRunning)));

            var gewinner = Enumerable.Range(0, teilnehmer).Where(i => ergebnis[i].Count == 0).ToList();
            var g = Assert.Single(gewinner);
            var alle = new OrdnerSpeicher(ordner).Reservierungen().Where(r => r.Element == gemeinsam || eigene.Contains(r.Element)).ToList();
            Assert.Equal(2, alle.Count);                           // nur die beiden des Gewinners, Verlierer haben zurückgenommen
            Assert.All(alle, r => Assert.Equal($"P{g}", r.Akteur));
            Assert.All(Enumerable.Range(0, teilnehmer).Where(i => i != g), i => Assert.Equal($"P{g}", Assert.Single(ergebnis[i]).Akteur));
        }
        Assert.Empty(Directory.GetFiles(Unterordner("sperren"), ".*"));
    }
}
