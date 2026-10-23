using System.Buffers.Text;
using System.Security.Cryptography;
using Hlks.Kern;

namespace Hlks.Web.Tests;

/// <summary>Kopplung (Quest App P3, XrKopplung.cs): Weg A (Paket), Weg B (Vergleichscode an Brille und PC, Bestätigung am PC) mit Zeitlimit und Versuchsgrenze, Widerruf, nur gehasht gespeichert.</summary>
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
        Assert.Throws<HlksFehler>(() => u.Kopplungen.Anfragen("", null, new XrTestBrille().Verpflichtung, "10.0.0.5"));
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

    // ---------------------------------------------------------------- Weg B: Vergleichscode (nur vergleichen, Christian 23.10.2026)

    const string Fp = "ab12cd34ef567890ab12cd34ef567890ab12cd34ef567890ab12cd34ef567890";
    const string Ip = "10.0.0.5";

    /// <summary>Anfrage und Aufdecken wie die Brille; gibt Anfrage, Brille und den Code zurück, den die Brille selbst rechnet.</summary>
    static (XrAnfrageStand Anfrage, XrTestBrille Brille, string CodeBrille) Aufgedeckt(Umgebung u, string brille = "QUEST-B", string? name = null, string ip = Ip, string fpBrille = Fp)
    {
        var b = new XrTestBrille();
        var a = u.Kopplungen.Anfragen(brille, name, b.Verpflichtung, ip);
        var e = u.Kopplungen.Aufdecken(a.Anfrage, b.ZufallText, ip, () => Fp);
        Assert.Equal((200, "vergleichen"), (e.Status, e.Zustand));
        return (a, b, XrTestBrille.Code(fpBrille, b.ZufallText, a.ZufallPc));
    }

    static string? CodeAmPc(Umgebung u, string anfrage) => u.Kopplungen.OffeneAnfragen().Single(x => x.Anfrage == anfrage).Code;

    [Fact]
    public void Weg_B_Brille_und_PC_zeigen_denselben_Code_nach_Bestaetigung_am_PC_gibt_es_das_Paket()
    {
        using var u = new Umgebung();
        var b = new XrTestBrille();
        var a = u.Kopplungen.Anfragen("QUEST-B", "Quest Gast", b.Verpflichtung, Ip);
        Assert.Matches("^[A-Za-z0-9_-]{22}$", a.Anfrage);
        Assert.Matches("^[A-Za-z0-9_-]{43}$", a.ZufallPc);
        Assert.Equal(u.Uhr.Jetzt + TimeSpan.FromMinutes(5), a.Bis);
        Assert.Null(CodeAmPc(u, a.Anfrage));   // vor dem Aufdecken gibt es keinen Code

        Assert.Equal(409, u.Kopplungen.Abholen(a.Anfrage, Ip, () => Info).Status);   // erst aufdecken
        Assert.Equal(409, Assert.Throws<XrFehler>(() => u.Kopplungen.Bestaetigen(a.Anfrage)).Status);   // nichts zu vergleichen

        var e = u.Kopplungen.Aufdecken(a.Anfrage, b.ZufallText, Ip, () => Fp);
        Assert.Equal((200, "vergleichen"), (e.Status, e.Zustand));
        Assert.Null(e.Paket);
        var amPc = CodeAmPc(u, a.Anfrage);
        Assert.Matches("^[0-9]{6}$", amPc!);
        Assert.Equal(XrTestBrille.Code(Fp, b.ZufallText, a.ZufallPc), amPc);   // die Brille rechnet unabhängig denselben Code
        Assert.Equal(XrKopplungen.Vergleichscode(Fp, b.ZufallText, a.ZufallPc), amPc);
        Assert.Equal(409, u.Kopplungen.Aufdecken(a.Anfrage, b.ZufallText, Ip, () => Fp).Status);   // nur einmal

        var w = u.Kopplungen.Abholen(a.Anfrage, Ip, () => Info);   // vor der Bestätigung am PC: warten
        Assert.Equal((202, "wartet"), (w.Status, w.Zustand));
        Assert.Null(w.Paket);
        Assert.Empty(u.Kopplungen.Liste());

        Assert.True(u.Kopplungen.Bestaetigen(a.Anfrage).Bestaetigt);
        var g = u.Kopplungen.Abholen(a.Anfrage, Ip, () => Info);
        Assert.Equal((200, "gekoppelt"), (g.Status, g.Zustand));
        Assert.Equal("QUEST-B", g.Paket!.Brille);
        Assert.Equal("Quest Gast", g.Paket.Name);
        Assert.Equal(Info.Fingerabdruck, g.Paket.Fingerabdruck);
        Assert.Equal("code", u.Kopplungen.Pruefen(Bearer(g.Paket))!.Weg);
        Assert.Empty(u.Kopplungen.OffeneAnfragen());
        Assert.Equal(404, u.Kopplungen.Abholen(a.Anfrage, Ip, () => Info).Status);   // nur einmal
    }

    [Fact]
    public void Vergleichscode_haengt_von_Fingerabdruck_und_beiden_Zufallszahlen_ab()
    {
        var zb = System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var zp = System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var code = XrKopplungen.Vergleichscode(Fp, zb, zp);
        Assert.Matches("^[0-9]{6}$", code);
        Assert.Equal(code, XrKopplungen.Vergleichscode(Fp, zb, zp));
        Assert.Equal(code, XrTestBrille.Code(Fp, zb, zp));
        // anderer Fingerabdruck (jemand dazwischen), andere Zufallszahl der Brille bzw. des PCs: anderer Code
        Assert.NotEqual(code, XrKopplungen.Vergleichscode(new string('0', 64), zb, zp));
        Assert.NotEqual(code, XrKopplungen.Vergleichscode(Fp, System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)), zp));
        Assert.NotEqual(code, XrKopplungen.Vergleichscode(Fp, zb, System.Buffers.Text.Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32))));
        Assert.NotEqual(code, XrKopplungen.Vergleichscode(Fp, zp, zb));   // Reihenfolge zählt
        // feste Werte (Gegenprobe für die App): SHA-256 von "hlks-xr-vergleich|1|<fp>|AAAA…|BBBB…"
        Assert.Equal(XrTestBrille.Code(Fp, new string('A', 43), new string('B', 43)), XrKopplungen.Vergleichscode(Fp, new string('A', 43), new string('B', 43)));
    }

    [Fact]
    public void Weg_B_jemand_dazwischen_sieht_einen_anderen_Code()
    {
        using var u = new Umgebung();
        // die Brille sieht das Zertifikat des Angreifers, KICAD rechnet mit dem eigenen
        var (a, _, codeBrille) = Aufgedeckt(u, fpBrille: "ff" + Fp[2..]);
        Assert.NotEqual(CodeAmPc(u, a.Anfrage), codeBrille);
        // dieselbe Brille mit dem echten Zertifikat sähe den Code des PCs
        var (a2, b2, codeEcht) = Aufgedeckt(u, ip: "10.0.0.9");
        Assert.Equal(CodeAmPc(u, a2.Anfrage), codeEcht);
        // die Codes zweier Anfragen sind verschieden (eigene Zufallszahlen)
        Assert.NotEqual(CodeAmPc(u, a.Anfrage), CodeAmPc(u, a2.Anfrage));
        Assert.NotEqual(a.ZufallPc, a2.ZufallPc);
        Assert.NotNull(b2);
    }

    [Fact]
    public void Weg_B_falsche_Zufallszahl_zaehlt_als_Versuch_und_sperrt_nach_fuenf()
    {
        using var u = new Umgebung();
        var b = new XrTestBrille();
        var a = u.Kopplungen.Anfragen("QUEST-B", null, b.Verpflichtung, Ip);
        var fremd = new XrTestBrille().ZufallText;   // passt nicht zur Verpflichtung
        for (int i = 1; i <= 4; i++)
        {
            var e = u.Kopplungen.Aufdecken(a.Anfrage, fremd, Ip, () => Fp);
            Assert.Equal((403, "falsch", 5 - i), (e.Status, e.Zustand, e.VersucheUebrig));
            Assert.Null(CodeAmPc(u, a.Anfrage));
        }
        Assert.Equal(400, u.Kopplungen.Aufdecken(a.Anfrage, "kurz", Ip, () => Fp).Status);   // ungültig: zählt nicht
        var letzte = u.Kopplungen.Aufdecken(a.Anfrage, fremd, Ip, () => Fp);
        Assert.Equal((410, "gesperrt"), (letzte.Status, letzte.Zustand));
        Assert.Equal(404, u.Kopplungen.Aufdecken(a.Anfrage, b.ZufallText, Ip, () => Fp).Status);   // auch die richtige hilft nicht mehr
        Assert.Empty(u.Kopplungen.OffeneAnfragen());
        Assert.Empty(u.Kopplungen.Liste());
    }

    [Fact]
    public void Weg_B_Zeitlimit()
    {
        using var u = new Umgebung();
        var (a, _, _) = Aufgedeckt(u);
        u.Uhr.Vor(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.Equal(404, Assert.Throws<XrFehler>(() => u.Kopplungen.Bestaetigen(a.Anfrage)).Status);
        Assert.Equal(404, u.Kopplungen.Abholen(a.Anfrage, Ip, () => Info).Status);   // abgelaufene Anfragen sind schon aufgeräumt
        Assert.Empty(u.Kopplungen.OffeneAnfragen());

        var (b, _, _) = Aufgedeckt(u);
        u.Kopplungen.Bestaetigen(b.Anfrage);
        u.Uhr.Vor(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        var eb = u.Kopplungen.Abholen(b.Anfrage, Ip, () => Info);
        Assert.Equal((410, "abgelaufen"), (eb.Status, eb.Zustand));
        Assert.Empty(u.Kopplungen.Liste());

        var c = u.Kopplungen.Anfragen("QUEST-C", null, new XrTestBrille().Verpflichtung, Ip);
        u.Uhr.Vor(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.Equal(410, u.Kopplungen.Aufdecken(c.Anfrage, new string('A', 43), Ip, () => Fp).Status);
    }

    [Fact]
    public void Weg_B_abgelehnt_oder_von_anderer_Adresse()
    {
        using var u = new Umgebung();
        var b = new XrTestBrille();
        var a = u.Kopplungen.Anfragen("QUEST-B", null, b.Verpflichtung, Ip);
        Assert.Equal(404, u.Kopplungen.Aufdecken(a.Anfrage, b.ZufallText, "10.0.0.6", () => Fp).Status);   // andere IP-Adresse
        Assert.Equal(200, u.Kopplungen.Aufdecken(a.Anfrage, b.ZufallText, Ip, () => Fp).Status);
        Assert.Equal(404, u.Kopplungen.Abholen(a.Anfrage, "10.0.0.6", () => Info).Status);
        u.Kopplungen.Ablehnen(a.Anfrage);   // z. B. weil die Codes verschieden sind
        Assert.Equal(409, Assert.Throws<XrFehler>(() => u.Kopplungen.Bestaetigen(a.Anfrage)).Status);
        var e = u.Kopplungen.Abholen(a.Anfrage, Ip, () => Info);
        Assert.Equal((403, "abgelehnt"), (e.Status, e.Zustand));
        Assert.Empty(u.Kopplungen.OffeneAnfragen());
        Assert.Empty(u.Kopplungen.Liste());
        Assert.Equal(404, u.Kopplungen.Abholen("nicht-da", Ip, () => Info).Status);
        Assert.Equal(404, u.Kopplungen.Abholen(null, Ip, () => Info).Status);
    }

    [Fact]
    public void Weg_B_Verpflichtung_muss_32_Byte_sein()
    {
        using var u = new Umgebung();
        Assert.Throws<HlksFehler>(() => u.Kopplungen.Anfragen("QUEST-B", null, null, Ip));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.Anfragen("QUEST-B", null, "abc", Ip));
        Assert.Throws<HlksFehler>(() => u.Kopplungen.Anfragen("QUEST-B", null, new string('B', 43), Ip));   // nicht kanonisch
        Assert.Throws<HlksFehler>(() => u.Kopplungen.Anfragen("QUEST-B", null, new string('A', 44), Ip));
        Assert.Empty(u.Kopplungen.OffeneAnfragen());
    }

    [Fact]
    public void Weg_B_hoechstens_drei_offene_Anfragen()
    {
        using var u = new Umgebung();
        string V() => new XrTestBrille().Verpflichtung;
        u.Kopplungen.Anfragen("Q1", null, V(), "10.0.0.1");
        u.Kopplungen.Anfragen("Q2", null, V(), "10.0.0.2");
        var c = u.Kopplungen.Anfragen("Q3", null, V(), "10.0.0.3");
        Assert.Equal(429, Assert.Throws<XrFehler>(() => u.Kopplungen.Anfragen("Q4", null, V(), "10.0.0.4")).Status);
        // dieselbe Adresse ersetzt ihre offene Anfrage
        var c2 = u.Kopplungen.Anfragen("Q3", null, V(), "10.0.0.3");
        Assert.NotEqual(c.Anfrage, c2.Anfrage);
        Assert.Equal(3, u.Kopplungen.OffeneAnfragen().Count);
        Assert.Equal(404, u.Kopplungen.Abholen(c.Anfrage, "10.0.0.3", () => Info).Status);
    }

    [Fact]
    public void Weg_B_hoechstens_zwanzig_neue_Anfragen_je_Stunde()
    {
        using var u = new Umgebung();
        for (int i = 0; i < 20; i++) u.Kopplungen.Anfragen($"Q{i}", null, new XrTestBrille().Verpflichtung, "10.0.2.1");   // gleiche Adresse: ersetzt, es bleibt eine offene
        Assert.Single(u.Kopplungen.OffeneAnfragen());
        Assert.Equal(429, Assert.Throws<XrFehler>(() => u.Kopplungen.Anfragen("Q21", null, new XrTestBrille().Verpflichtung, "10.0.2.2")).Status);
        u.Uhr.Vor(TimeSpan.FromHours(1));
        u.Kopplungen.Anfragen("Q22", null, new XrTestBrille().Verpflichtung, "10.0.2.2");
    }
}
