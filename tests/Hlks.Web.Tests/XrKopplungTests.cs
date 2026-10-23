using System.Buffers.Text;
using System.Security.Cryptography;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>Kopplung (Quest App P3, XrKopplung.cs): Weg A (Paket), Weg B (Code am PC) mit Zeitlimit und Versuchsgrenze, Widerruf, nur gehasht gespeichert.</summary>
public class XrKopplungTests
{
    static readonly XrZugangInfo Info = new("PC-TEST", ["192.168.1.20"], 5140, 5141, new string('a', 64));

    sealed class Umgebung : IDisposable
    {
        public string Ordner { get; } = Path.Combine(Path.GetTempPath(), "hlks_xrk_" + Guid.NewGuid().ToString("N")[..10]);
        public XrTestUhr Uhr { get; } = new();
        public XrEinstellungen Einstellungen { get; }
        public XrKopplungen Kopplungen { get; }
        public List<string> Entfernt { get; } = [];
        public Umgebung()
        {
            Einstellungen = new XrEinstellungen { Ordner = Ordner, Uhr = () => Uhr.Jetzt };
            Kopplungen = new XrKopplungen(Einstellungen);
            Kopplungen.KopplungEntfernt += Entfernt.Add;
        }
        public void Dispose() { try { if (Directory.Exists(Ordner)) Directory.Delete(Ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }

    static string Bearer(XrPaket p) => $"Bearer {p.Kopplung}.{p.Schluessel}";

    // ---------------------------------------------------------------- Weg A

    [Fact]
    public void Weg_A_Paket_enthaelt_alles_was_die_App_braucht()
    {
        using var u = new Umgebung();
        var p = u.Kopplungen.PaketErzeugen("1WMHH000X00000", "Quest Christian", Info);
        Assert.Equal("hlks-xr-kopplung", p.Art);
        Assert.Equal(1, p.Fassung);
        Assert.Equal("PC-TEST", p.Pc);
        Assert.Equal(["192.168.1.20"], p.Ips);
        Assert.Equal(5140, p.Port);
        Assert.Equal(5141, p.SuchPort);
        Assert.Equal(Info.Fingerabdruck, p.Fingerabdruck);
        Assert.Equal("1WMHH000X00000", p.Brille);
        Assert.Equal("Quest Christian", p.Name);
        Assert.Matches("^[0-9a-f]{16}$", p.Kopplung);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", p.Schluessel);
        var json = Json.Text(p);   // so geht es an die App (camelCase)
        foreach (var feld in new[] { "\"pc\"", "\"ips\"", "\"port\"", "\"suchPort\"", "\"fingerabdruck\"", "\"kopplung\"", "\"schluessel\"", "\"brille\"", "\"name\"" })
            Assert.Contains(feld, json);
        var k = u.Kopplungen.Pruefen(Bearer(p));
        Assert.NotNull(k);
        Assert.Equal(p.Kopplung, k!.Id);
        Assert.Equal("paket", k.Weg);
        Assert.NotNull(u.Kopplungen.Zuletzt(p.Kopplung));
    }

    [Fact]
    public void Der_Schluessel_wird_nur_gehasht_gespeichert()
    {
        using var u = new Umgebung();
        var p = u.Kopplungen.PaketErzeugen("QUEST-A", null, Info);
        var datei = File.ReadAllText(u.Kopplungen.DateiPfad);
        var roh = XrSchluessel.AusText(p.Schluessel)!;
        Assert.DoesNotContain(p.Schluessel, datei);
        Assert.DoesNotContain(Convert.ToBase64String(roh), datei);
        Assert.DoesNotContain(Convert.ToHexString(roh), datei, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Convert.ToBase64String(XrSchluessel.Pruefwert(roh)), datei);   // nur der Prüfwert (HMAC)
        if (OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain(Convert.ToBase64String(XrSchluessel.Suchschluessel(roh)), datei);   // Suchschlüssel nur mit DPAPI
            Assert.Contains("\"suche\": \"dpapi:", datei);
        }
        Assert.Contains("\"brille\": \"QUEST-A\"", datei);
        Assert.Equal("QUEST-A", p.Name);   // ohne Namen: die Kennung
    }

    [Fact]
    public void Falscher_oder_verformter_Schluessel_gilt_nicht()
    {
        using var u = new Umgebung();
        var p = u.Kopplungen.PaketErzeugen("QUEST-A", null, Info);
        var anderer = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        Assert.Null(u.Kopplungen.Pruefen(null));
        Assert.Null(u.Kopplungen.Pruefen(""));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}.{anderer}"));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer 0123456789abcdef.{p.Schluessel}"));
        Assert.Null(u.Kopplungen.Pruefen($"Basic {p.Kopplung}.{p.Schluessel}"));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}{p.Schluessel}"));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}.{p.Schluessel}x"));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung.ToUpperInvariant()}.{p.Schluessel}"));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}.{p.Schluessel}=="));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}.{new string('B', 43)}"));   // nicht kanonisches Endzeichen: null, keine Ausnahme
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}.{new string('/', 43)}"));
        Assert.Null(u.Kopplungen.Pruefen($"Bearer {p.Kopplung}.{p.Schluessel}" + new string(' ', 200)));
        Assert.NotNull(u.Kopplungen.Pruefen($"bearer {p.Kopplung}.{p.Schluessel}"));   // Art ohne Rücksicht auf Gross-/Kleinschreibung
    }

    [Fact]
    public void Widerruf_entfernt_die_Kopplung_und_meldet_sie()
    {
        using var u = new Umgebung();
        var p = u.Kopplungen.PaketErzeugen("QUEST-A", null, Info);
        Assert.True(u.Kopplungen.Widerrufen(p.Kopplung));
        Assert.Null(u.Kopplungen.Pruefen(Bearer(p)));
        Assert.Empty(u.Kopplungen.Liste());
        Assert.Equal([p.Kopplung], u.Entfernt);
        Assert.False(u.Kopplungen.Widerrufen(p.Kopplung));
        Assert.False(u.Kopplungen.Widerrufen("../../etwas"));
        Assert.DoesNotContain(p.Kopplung, File.ReadAllText(u.Kopplungen.DateiPfad));
    }

    [Fact]
    public void Neue_Kopplung_derselben_Brille_ersetzt_die_alte()
    {
        using var u = new Umgebung();
        var a = u.Kopplungen.PaketErzeugen("QUEST-A", null, Info);
        var b = u.Kopplungen.PaketErzeugen("quest-a", null, Info);
        Assert.Null(u.Kopplungen.Pruefen(Bearer(a)));
        Assert.NotNull(u.Kopplungen.Pruefen(Bearer(b)));
        Assert.Single(u.Kopplungen.Liste());
        Assert.Equal([a.Kopplung], u.Entfernt);
    }

    [Fact]
    public void Hoechstens_acht_Brillen()
    {
        using var u = new Umgebung();
        for (int i = 0; i < 8; i++) u.Kopplungen.PaketErzeugen($"QUEST-{i}", null, Info);
        var f = Assert.Throws<XrFehler>(() => u.Kopplungen.PaketErzeugen("QUEST-9", null, Info));
        Assert.Equal(409, f.Status);
        Assert.Equal(8, u.Kopplungen.Liste().Count);
    }

    [Fact]
    public void Ungueltige_Kennung_oder_Name_wird_abgelehnt()
    {
        using var u = new Umgebung();
        Assert.Throws<HlksFehler>(() => u.Kopplungen.PaketErzeugen(null, null, Info));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.PaketErzeugen("-schalter", null, Info));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.PaketErzeugen("a b", null, Info));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.PaketErzeugen(new string('A', 65), null, Info));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.PaketErzeugen("QUEST-A", new string('n', 61), Info));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.PaketErzeugen("QUEST-A", "Zeile\nzwei", Info));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.Anfragen("", null, "10.0.0.5"));
        Assert.Empty(u.Kopplungen.Liste());
    }

    [Fact]
    public void Ein_zweiter_KICAD_Prozess_sieht_Kopplungen_und_Widerruf()
    {
        using var u = new Umgebung();
        var p = u.Kopplungen.PaketErzeugen("QUEST-A", null, Info);
        var zweiter = new XrKopplungen(u.Einstellungen);
        Assert.NotNull(zweiter.Pruefen(Bearer(p)));
        Assert.NotNull(zweiter.Suchschluessel(p.Kopplung));
        u.Kopplungen.Widerrufen(p.Kopplung);
        Thread.Sleep(1100);   // der zweite sieht höchstens einmal je Sekunde nach
        Assert.Null(zweiter.Pruefen(Bearer(p)));
    }

    [Fact]
    public void Kaputte_Ablage_heisst_keine_Kopplung()
    {
        using var u = new Umgebung();
        Directory.CreateDirectory(u.Ordner);
        File.WriteAllText(u.Kopplungen.DateiPfad, "{ kaputt");
        Assert.Empty(new XrKopplungen(u.Einstellungen).Liste());
        File.WriteAllText(u.Kopplungen.DateiPfad, "{\"format\":1,\"kopplungen\":[{\"id\":\"zz\",\"brille\":\"A\",\"pruefwert\":\"AAAA\"}]}");
        Assert.Empty(new XrKopplungen(u.Einstellungen).Liste());
    }

    // ---------------------------------------------------------------- Weg B

    [Fact]
    public void Weg_B_Code_am_PC_bestaetigen_dann_abholen()
    {
        using var u = new Umgebung();
        var a = u.Kopplungen.Anfragen("QUEST-B", "Quest Gast", "10.0.0.5");
        Assert.Matches("^[0-9]{6}$", a.Code);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", a.Anfrage);
        Assert.Equal(u.Uhr.Jetzt + TimeSpan.FromMinutes(5), a.Bis);
        var offen = Assert.Single(u.Kopplungen.OffeneAnfragen());
        Assert.Equal(a.Code, offen.Code);

        var falsch = a.Code == "000000" ? "000001" : "000000";
        var e1 = u.Kopplungen.Abholen(a.Anfrage, falsch, "10.0.0.5", () => Info);
        Assert.Equal((403, "falsch", 4), (e1.Status, e1.Zustand, e1.VersucheUebrig));
        Assert.Null(e1.Paket);

        var e2 = u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.5", () => Info);   // Code richtig, aber am PC noch nicht bestätigt
        Assert.Equal((202, "wartet"), (e2.Status, e2.Zustand));
        Assert.Null(e2.Paket);
        Assert.Empty(u.Kopplungen.Liste());

        var e0 = u.Kopplungen.Abholen(a.Anfrage, null, "10.0.0.5", () => Info);   // ohne Code: zählt nicht als Versuch
        Assert.Equal(400, e0.Status);
        Assert.Equal(1, Assert.Single(u.Kopplungen.OffeneAnfragen()).Versuche);

        Assert.True(u.Kopplungen.Bestaetigen(a.Anfrage).Bestaetigt);
        var e3 = u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.5", () => Info);
        Assert.Equal((200, "gekoppelt"), (e3.Status, e3.Zustand));
        Assert.Equal("QUEST-B", e3.Paket!.Brille);
        Assert.Equal("Quest Gast", e3.Paket.Name);
        Assert.Equal("code", u.Kopplungen.Pruefen(Bearer(e3.Paket))!.Weg);
        Assert.Empty(u.Kopplungen.OffeneAnfragen());

        var e4 = u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.5", () => Info);   // nur einmal
        Assert.Equal(404, e4.Status);
    }

    [Fact]
    public void Weg_B_Versuchsgrenze_sperrt_die_Anfrage()
    {
        using var u = new Umgebung();
        var a = u.Kopplungen.Anfragen("QUEST-B", null, "10.0.0.5");
        u.Kopplungen.Bestaetigen(a.Anfrage);
        var falsch = a.Code == "999999" ? "999998" : "999999";
        for (int i = 1; i <= 4; i++)
        {
            var e = u.Kopplungen.Abholen(a.Anfrage, falsch, "10.0.0.5", () => Info);
            Assert.Equal((403, 5 - i), (e.Status, e.VersucheUebrig));
        }
        var letzte = u.Kopplungen.Abholen(a.Anfrage, falsch, "10.0.0.5", () => Info);
        Assert.Equal((410, "gesperrt"), (letzte.Status, letzte.Zustand));
        Assert.Equal(404, u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.5", () => Info).Status);   // auch der richtige Code hilft nicht mehr
        Assert.Empty(u.Kopplungen.Liste());
    }

    [Fact]
    public void Weg_B_Zeitlimit()
    {
        using var u = new Umgebung();
        var a = u.Kopplungen.Anfragen("QUEST-B", null, "10.0.0.5");
        u.Uhr.Vor(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.Equal(404, Assert.Throws<XrFehler>(() => u.Kopplungen.Bestaetigen(a.Anfrage)).Status);
        var e = u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.5", () => Info);
        Assert.Equal(404, e.Status);   // abgelaufene Anfragen sind schon aufgeräumt
        Assert.Empty(u.Kopplungen.OffeneAnfragen());

        var b = u.Kopplungen.Anfragen("QUEST-B", null, "10.0.0.5");
        u.Kopplungen.Bestaetigen(b.Anfrage);
        u.Uhr.Vor(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var eb = u.Kopplungen.Abholen(b.Anfrage, b.Code, "10.0.0.5", () => Info);
        Assert.Equal((410, "abgelaufen"), (eb.Status, eb.Zustand));
        Assert.Empty(u.Kopplungen.Liste());
    }

    [Fact]
    public void Weg_B_abgelehnt_oder_von_anderer_Adresse()
    {
        using var u = new Umgebung();
        var a = u.Kopplungen.Anfragen("QUEST-B", null, "10.0.0.5");
        Assert.Equal(404, u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.6", () => Info).Status);   // andere IP-Adresse
        u.Kopplungen.Ablehnen(a.Anfrage);
        var e = u.Kopplungen.Abholen(a.Anfrage, a.Code, "10.0.0.5", () => Info);
        Assert.Equal((403, "abgelehnt"), (e.Status, e.Zustand));
        Assert.Empty(u.Kopplungen.OffeneAnfragen());
        Assert.Equal(404, u.Kopplungen.Abholen("nicht-da", "123456", "10.0.0.5", () => Info).Status);
        Assert.Equal(404, u.Kopplungen.Abholen(null, "123456", "10.0.0.5", () => Info).Status);
    }

    [Fact]
    public void Weg_B_hoechstens_drei_offene_Anfragen()
    {
        using var u = new Umgebung();
        u.Kopplungen.Anfragen("Q1", null, "10.0.0.1");
        u.Kopplungen.Anfragen("Q2", null, "10.0.0.2");
        var c = u.Kopplungen.Anfragen("Q3", null, "10.0.0.3");
        Assert.Equal(429, Assert.Throws<XrFehler>(() => u.Kopplungen.Anfragen("Q4", null, "10.0.0.4")).Status);
        // dieselbe Adresse ersetzt ihre offene Anfrage
        var c2 = u.Kopplungen.Anfragen("Q3", null, "10.0.0.3");
        Assert.NotEqual(c.Anfrage, c2.Anfrage);
        Assert.Equal(3, u.Kopplungen.OffeneAnfragen().Count);
        Assert.Equal(404, u.Kopplungen.Abholen(c.Anfrage, c.Code, "10.0.0.3", () => Info).Status);
    }

    [Fact]
    public void Weg_B_hoechstens_zwanzig_neue_Anfragen_je_Stunde()
    {
        using var u = new Umgebung();
        for (int i = 0; i < 20; i++) u.Kopplungen.Anfragen($"Q{i}", null, "10.0.2.1");   // gleiche Adresse: ersetzt, es bleibt eine offene
        Assert.Single(u.Kopplungen.OffeneAnfragen());
        Assert.Equal(429, Assert.Throws<XrFehler>(() => u.Kopplungen.Anfragen("Q21", null, "10.0.2.2")).Status);
        u.Uhr.Vor(TimeSpan.FromHours(1));
        u.Kopplungen.Anfragen("Q22", null, "10.0.2.2");
    }
}
