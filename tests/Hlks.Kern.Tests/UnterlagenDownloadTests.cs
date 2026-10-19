using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;
using static Hlks.Kern.Tests.UnterlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Kleiner HTTP-Server nur für die Tests (Loopback, kein echtes Internet): Weg → Antwort.</summary>
sealed class TestServer : IDisposable
{
    public sealed record Antwort(int Status, byte[] Body, string? Typ = null, Dictionary<string, string>? Kopf = null, TimeSpan? Verzoegerung = null, bool OhneLaenge = false);

    readonly TcpListener l = new(IPAddress.Loopback, 0);
    readonly CancellationTokenSource ende = new();
    public int Port => ((IPEndPoint)l.LocalEndpoint).Port;
    public string Basis => $"http://127.0.0.1:{Port}";
    public ConcurrentQueue<(string Pfad, Dictionary<string, string> Kopf)> Anfragen { get; } = new();
    public Func<string, Antwort?> Weg { get; set; } = _ => null;

    public TestServer()
    {
        l.Start();
        _ = Task.Run(async () =>
        {
            while (!ende.IsCancellationRequested)
            {
                TcpClient c;
                try { c = await l.AcceptTcpClientAsync(ende.Token); } catch { return; }
                _ = Task.Run(() => Bedienen(c));
            }
        });
    }

    async Task Bedienen(TcpClient c)
    {
        try
        {
            using var _ = c;
            var s = c.GetStream();
            var sb = new StringBuilder(); var b = new byte[1];
            while (!sb.ToString().EndsWith("\r\n\r\n")) { if (await s.ReadAsync(b, ende.Token) == 0) return; sb.Append((char)b[0]); }
            var zeilen = sb.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            var pfad = zeilen[0].Split(' ')[1];
            var kopf = zeilen.Skip(1).Select(z => z.Split(':', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0].Trim(), x => x[1].Trim(), StringComparer.OrdinalIgnoreCase);
            Anfragen.Enqueue((pfad, kopf));
            var a = Weg(pfad) ?? new Antwort(404, "nicht da"u8.ToArray());
            if (a.Verzoegerung is { } d) await Task.Delay(d, ende.Token);
            var h = new StringBuilder($"HTTP/1.1 {a.Status} X\r\n");
            if (a.Typ is not null) h.Append($"Content-Type: {a.Typ}\r\n");
            foreach (var (k, v) in a.Kopf ?? []) h.Append($"{k}: {v}\r\n");
            if (!a.OhneLaenge) h.Append($"Content-Length: {a.Body.Length}\r\n");
            h.Append("Connection: close\r\n\r\n");
            await s.WriteAsync(Encoding.ASCII.GetBytes(h.ToString()), ende.Token);
            await s.WriteAsync(a.Body, ende.Token);
            await s.FlushAsync(ende.Token);
        }
        catch { /* Verbindung vom Client geschlossen (Abbruch bei Grenzen) */ }
    }

    public void Dispose() { ende.Cancel(); l.Stop(); }
}

[CollectionDefinition("UnterlagenDownload", DisableParallelization = true)]
public sealed class UnterlagenDownloadSammlung;

/// <summary>unterlage_aus_url (Modul 2, Internet-Recherche) über einen lokalen Test-Server: Schutz, Grenzen, Art nach Inhalt, ZIP. Kein echtes Internet.</summary>
[Collection("UnterlagenDownload")]
public sealed class UnterlagenDownloadTests : IDisposable
{
    readonly TestServer s = new();
    readonly long dateiMax = UnterlagenDownload.MaxDateiBytes, eintragMax = UnterlagenDownload.MaxEintragBytes, entpMax = UnterlagenDownload.MaxEntpacktBytes;
    readonly int eintraegeMax = UnterlagenDownload.MaxEintraege;
    readonly TimeSpan zeit = UnterlagenDownload.Zeitlimit;

    public UnterlagenDownloadTests()
    {
        UnterlagenDownload.TestAusnahme = (ip, port) => IPAddress.IsLoopback(ip) && port == s.Port;   // nur der Test-Server, nichts sonst
    }

    public void Dispose()
    {
        UnterlagenDownload.TestAusnahme = null;
        UnterlagenDownload.MaxDateiBytes = dateiMax; UnterlagenDownload.MaxEintragBytes = eintragMax; UnterlagenDownload.MaxEntpacktBytes = entpMax;
        UnterlagenDownload.MaxEintraege = eintraegeMax; UnterlagenDownload.Zeitlimit = zeit;
        s.Dispose();
    }

    static BefehlsErgebnis Holen(Projekt p, string url, string? art = null, bool trocken = false)
    {
        var par = new JsonObject { ["url"] = url };
        if (art is not null) par["art"] = art;
        return p.Ausfuehren("unterlage_aus_url", par, trocken);
    }

    static string Zusammenfassung(BefehlsErgebnis e) =>
        e.Hinweise.Single(h => h.StartsWith("Zusammenfassung (wie unterlagen_lesen): ")).Substring("Zusammenfassung (wie unterlagen_lesen): ".Length);

    static byte[] Zip(params (string Name, byte[] Inhalt)[] eintraege)
    {
        using var ms = new MemoryStream();
        using (var z = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (n, i) in eintraege) { var e = z.CreateEntry(n, CompressionLevel.Optimal); using var w = e.Open(); w.Write(i); }
        return ms.ToArray();
    }

    static byte[] Pdf(string name = "d.pdf") { using var o = new Ordner(); return File.ReadAllBytes(TestPdf(o, name)); }
    static readonly byte[] Step = Encoding.ASCII.GetBytes("ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('CONFIG_CONTROL_DESIGN'));\nENDSEC;\nDATA;\nENDSEC;\nEND-ISO-10303-21;\n");
    static byte[] Exe() => [.. "MZ"u8.ToArray(), .. new byte[500]];
    static byte[] PdfMitNullen(int n) => [.. "%PDF-1.4\n"u8.ToArray(), .. new byte[n]];

    // ---------- Laden und Lesen ----------

    [Fact]
    public void Pdf_laden_ablegen_und_lesen()
    {
        var pdf = Pdf();
        s.Weg = pfad => pfad == "/doc/datenblatt.pdf" ? new TestServer.Antwort(200, pdf, "application/pdf") : null;
        var p = Leer();
        var e = Ok(Holen(p, s.Basis + "/doc/datenblatt.pdf"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal("pdf", ul.Text("art"));
        Assert.Equal("familie", ul.Text("zweck"));
        Assert.Equal(s.Basis + "/doc/datenblatt.pdf", ul.Text("original"));
        Assert.Matches(@"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\dZ$", ul.Text("abgerufen")!);
        var ablName = Unterlagen.OriginalName(ul.Text("datei")!);   // Host, Pfad und Prüfsumme der URL, nicht der Dateiname der Quelle
        Assert.Matches(@"^127-0-0-1-doc-datenblatt-pdf_[0-9a-f]{8}.pdf$", ablName);
        Assert.Null(ul.Geschoss);
        Assert.Equal(System.Security.Cryptography.SHA256.HashData(pdf).Select(b => b.ToString("x2")).Aggregate("", (a, b) => a + b)[..12], ul.Text("stand"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("UL-1: ") && h.Contains("(pdf,") && h.Contains(ul.Text("stand")!));
        var z = JsonNode.Parse(Zusammenfassung(e))!["unterlagen"]![0]!;
        Assert.Equal("pdf", z["art"]!.GetValue<string>());
        Assert.Equal(2, z["jeArt"]!["tabelle"]!.GetValue<int>());
        // gelesen wie jede Unterlage
        var a = (JsonObject)Json.Knoten(Tuer.Abfrage(p, "befunde", new JsonObject { ["unterlage"] = "UL-1", ["art"] = "zelle", ["text"] = "67" }));
        Assert.Equal(1, a["treffer"]!.GetValue<int>());
        // User-Agent, keine Cookies
        var anf = s.Anfragen.Single();
        Assert.Equal("HLKS-CAD/0.1 (H+K Planungs AG)", anf.Kopf["User-Agent"]);
        Assert.DoesNotContain("Cookie", anf.Kopf.Keys);
    }

    [Fact]
    public void Bezug_zur_Familie_und_Trockenlauf()
    {
        s.Weg = _ => new TestServer.Antwort(200, Pdf(), "application/pdf");
        var p = Leer();
        var t = Ok(Holen(p, s.Basis + "/x.pdf", trocken: true));
        Assert.Contains(t.Hinweise, h => h.Contains("Es wurde nichts geladen"));
        Assert.Empty(s.Anfragen);
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        var nein = Nein(p.Ausfuehren("unterlage_aus_url", new JsonObject { ["url"] = s.Basis + "/x.pdf", ["familie"] = "FAM-99" }));
        Assert.Contains("FAM-99", nein.Fehler!);
    }

    [Fact]
    public void Html_mit_Tabelle_und_Links_wird_gelesen()
    {
        var html = """
            <!DOCTYPE html><html><head><title>Kugelhahn R2015 – Belimo</title><style>p{}</style><script>var x = "Skript";</script></head><body>
            <h1>Kugelhahn R2015</h1>
            <p>Auf/Zu-Kugelhahn, 2-Weg.<br>Innengewinde Rp.</p>
            <table id="massetabelle"><caption>Masse</caption>
              <thead><tr><th>Typ</th><th colspan="2">Masse</th></tr><tr><th></th><th>L [mm]</th><th>H [mm]</th></tr></thead>
              <tbody><tr><td rowspan="2">R2015</td><td>67</td><td>35</td></tr><tr><td>78</td><td>46</td></tr></tbody></table>
            <p><a href="/files/datenblatt.pdf">Datenblatt (PDF)</a> <a href="cad/ventil.dwg">CAD</a> <a href="https://andere.ch/seite">Seite</a> <a href="#oben">oben</a></p>
            <img src="/bilder/ventil.png" alt="Ventil">
            </body></html>
            """;
        s.Weg = pfad => pfad == "/produkt/r2015.html" ? new TestServer.Antwort(200, Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8") : null;
        var p = Leer();
        var e = Ok(Holen(p, s.Basis + "/produkt/r2015.html"));
        var ul = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal("html", ul.Text("art"));
        var u = UnterlagenService.Aufloesen(p, "UL-1");
        var b = UnterlagenService.Lesen(p, u).Befunde;
        Assert.Equal("html 1", b.Leser);
        Assert.All(b.Befunde, x => { Assert.Equal(1, x.Seite); Assert.Null(x.Lage); });
        Assert.Equal("Kugelhahn R2015 – Belimo", Art(b, "text").First(x => x.Zusatz?["titel"] is not null).Text());
        Assert.Contains(Art(b, "text"), x => x.Text() == "Kugelhahn R2015" && x.Zusatz!["tag"]!.GetValue<string>() == "h1");
        Assert.Contains(Art(b, "text"), x => x.Text() == "Auf/Zu-Kugelhahn, 2-Weg.\nInnengewinde Rp.");
        Assert.Contains(Art(b, "zeile"), x => x.Text() == "Innengewinde Rp.");
        Assert.DoesNotContain(b.Befunde, x => x.Text().Contains("Skript"));
        // Tabelle: Kopf aus th (colspan auf beide Spalten), rowspan wie gesehen
        var t = Art(b, "tabelle").Single();
        var w = (JsonObject)t.Wert!;
        Assert.Equal(["Typ", "Masse L [mm]", "Masse H [mm]"], ((JsonArray)w["kopf"]!).Select(x => x!.GetValue<string>()).ToArray());
        Assert.Equal(2, w["kopfZeilen"]!.GetValue<int>());
        var daten = ((JsonArray)w["daten"]!).Select(r => ((JsonArray)r!).Select(x => x!.GetValue<string>()).ToArray()).ToArray();
        Assert.Equal(["R2015", "67", "35"], daten[0]);
        Assert.Equal(["", "78", "46"], daten[1]);        // R2015 gilt für beide Zeilen: nur oben links
        var zus = (JsonArray)t.Zusatz!["zusammengefasst"]!;
        Assert.Equal(1, zus.Count);
        Assert.Equal(2, zus[0]!["zeilen"]!.GetValue<int>());
        Assert.Equal("Masse", t.Zusatz["titel"]!.GetValue<string>());
        Assert.Contains("table#massetabelle", t.Zusatz["pfad"]!.GetValue<string>());
        var l = Zelle(b, t.Id, 1, 2);
        Assert.Equal(67, Zahl(l.Wert)); Assert.Equal("mm", l.Einheit); Assert.Equal("Masse L [mm]", l.Zusatz!["kopf"]!.GetValue<string>());
        Assert.True(Zelle(b, t.Id, 1, 1).Zusatz!["zusammengefasst"]!.GetValue<bool>());
        // Links absolut, mit Ziel
        var links = Art(b, "link").ToList();
        Assert.Contains(links, x => x.Wert!["url"]!.GetValue<string>() == s.Basis + "/files/datenblatt.pdf" && x.Wert["text"]!.GetValue<string>() == "Datenblatt (PDF)" && x.Zusatz!["ziel"]!.GetValue<string>() == "pdf");
        Assert.Contains(links, x => x.Wert!["url"]!.GetValue<string>() == s.Basis + "/produkt/cad/ventil.dwg" && x.Zusatz!["ziel"]!.GetValue<string>() == "dwg");
        Assert.Contains(links, x => x.Wert!["url"]!.GetValue<string>() == "https://andere.ch/seite" && x.Zusatz!["ziel"]!.GetValue<string>() == "seite");
        Assert.DoesNotContain(links, x => x.Text().Contains("#oben"));
        var bild = Art(b, "bild").Single();
        Assert.Equal(s.Basis + "/bilder/ventil.png", bild.Wert!["src"]!.GetValue<string>());
        Assert.Equal("Ventil", bild.Wert["alt"]!.GetValue<string>());
        // unterlagen_lesen nennt die Datei-Links; unterlage_bild: klarer Hinweis
        var z = JsonNode.Parse(Zusammenfassung(e))!["unterlagen"]![0]!;
        Assert.Equal(2, z["dateiLinks"]!.AsArray().Count);
        var ex = Assert.Throws<HlksFehler>(() => Tuer.Abfrage(p, "unterlage_bild", new JsonObject { ["unterlage"] = "UL-1" }));
        Assert.Contains("unterlage_aus_url", ex.Vorschlag);   // kein WebFetch für die KI (Entscheid Hauptlauf 18.10.2026)
        Assert.DoesNotContain("WebFetch", ex.Vorschlag);
    }

    // ---------- Schutz ----------

    [Theory]
    [InlineData("http://10.1.2.3/x.pdf")]
    [InlineData("http://192.168.1.10/x.pdf")]
    [InlineData("http://172.20.0.1/x.pdf")]
    [InlineData("http://169.254.169.254/latest/")]
    [InlineData("http://0.0.0.0/x")]
    [InlineData("http://127.0.0.2/x")]
    [InlineData("http://[::1]/x")]
    [InlineData("http://[fd00::1]/x")]
    [InlineData("http://[fe80::1]/x")]
    [InlineData("http://[::ffff:10.0.0.5]/x")]
    [InlineData("http://localhost/x")]
    [InlineData("http://intranet/x")]
    [InlineData("http://nas.local/x")]
    [InlineData("http://user:geheim@hersteller.ch/x.pdf")]
    [InlineData("ftp://hersteller.ch/x.pdf")]
    [InlineData("file:///C:/Windows/win.ini")]
    public void Adressen_im_eigenen_Netz_und_andere_Schemata_werden_abgelehnt(string url)
    {
        var p = Leer();
        var n = Nein(Holen(p, url));
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        Assert.Empty(s.Anfragen);
    }

    [Fact]
    public void Localhost_mit_dem_Port_des_Test_Servers_ist_trotz_Ausnahme_gesperrt_und_ein_anderer_Loopback_Port_auch()
    {
        s.Weg = _ => new TestServer.Antwort(200, Pdf(), "application/pdf");
        var p = Leer();
        Nein(Holen(p, $"http://localhost:{s.Port}/x.pdf"));          // Name ohne Punkt
        Nein(Holen(p, $"http://127.0.0.1:{s.Port + 1}/x.pdf"));      // nicht der Port der Ausnahme
        Assert.Empty(s.Anfragen);
    }

    [Fact]
    public void Gesperrte_Adressbereiche_Einheit()
    {
        UnterlagenDownload.TestAusnahme = null;
        foreach (var a in new[] { "127.0.0.1", "10.0.0.1", "10.255.255.255", "172.16.0.1", "172.31.255.255", "192.168.0.1", "169.254.1.1", "0.0.0.0", "100.64.0.1", "224.0.0.1", "255.255.255.255",
                     "::1", "::", "fc00::1", "fdff::1", "fe80::1", "febf::1", "ff02::1", "::ffff:192.168.1.1", "::ffff:127.0.0.1" })
            Assert.True(UnterlagenDownload.Gesperrt(IPAddress.Parse(a), 80), a);
        foreach (var a in new[] { "8.8.8.8", "1.1.1.1", "172.15.0.1", "172.32.0.1", "193.168.0.1", "100.63.0.1", "2001:4860:4860::8888", "2a00:1450:4001::1" })
            Assert.False(UnterlagenDownload.Gesperrt(IPAddress.Parse(a), 80), a);
    }

    [Fact]
    public void Weiterleitung_auf_Loopback_wird_trotz_Ausnahme_fuer_den_Test_Server_abgelehnt()
    {
        s.Weg = pfad => pfad == "/r" ? new TestServer.Antwort(302, [], Kopf: new() { ["Location"] = $"http://127.0.0.1:{s.Port + 1}/geheim.pdf" }) : null;
        var p = Leer();
        var n = Nein(Holen(p, s.Basis + "/r"));
        Assert.Contains("eigenen Netz", n.Fehler!);
        Assert.Single(s.Anfragen);                                    // nur die erste Adresse wurde angefragt
        // Weiterleitung auf eine Adresse im Heimnetz und auf localhost: schon vor dem Verbinden abgelehnt
        foreach (var ziel in new[] { "http://10.0.0.5/x.pdf", "http://localhost/x.pdf", "ftp://x.ch/y", "http://[::1]/x" })
        {
            s.Weg = pfad => pfad == "/r" ? new TestServer.Antwort(301, [], Kopf: new() { ["Location"] = ziel }) : null;
            Nein(Holen(p, s.Basis + "/r"));
        }
    }

    [Fact]
    public void Weiterleitung_zum_selben_Server_wird_verfolgt_hoechstens_fuenf_und_ohne_Cookies()
    {
        var pdf = Pdf();
        s.Weg = pfad => pfad switch
        {
            "/a" => new TestServer.Antwort(302, [], Kopf: new() { ["Location"] = "/b", ["Set-Cookie"] = "sitzung=1" }),
            "/b" => new TestServer.Antwort(301, [], Kopf: new() { ["Location"] = s.Basis + "/c.pdf" }),
            "/c.pdf" => new TestServer.Antwort(200, pdf, "application/pdf"),
            "/schleife" => new TestServer.Antwort(302, [], Kopf: new() { ["Location"] = "/schleife" }),
            _ => null,
        };
        var p = Leer();
        var e = Ok(Holen(p, s.Basis + "/a"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Weitergeleitet auf") && h.Contains("/c.pdf"));
        Assert.Equal(s.Basis + "/a", p.Modell.DerKlasse("Unterlage").Single().Text("original"));
        Assert.All(s.Anfragen, a => Assert.DoesNotContain("Cookie", a.Kopf.Keys));
        var n = Nein(Holen(p, s.Basis + "/schleife"));
        Assert.Contains("Weiterleitungen", n.Fehler!);
        Assert.Equal(6, s.Anfragen.Count(a => a.Pfad == "/schleife"));   // die Anfrage und fünf Weiterleitungen
    }

    [Fact]
    public void Zu_grosse_Datei_wird_abgelehnt_mit_und_ohne_Content_Length()
    {
        UnterlagenDownload.MaxDateiBytes = 100 * 1024;
        var gross = PdfMitNullen(300 * 1024);
        s.Weg = pfad => pfad switch
        {
            "/mit.pdf" => new TestServer.Antwort(200, gross, "application/pdf"),
            "/ohne.pdf" => new TestServer.Antwort(200, gross, "application/pdf", OhneLaenge: true),
            "/klein.pdf" => new TestServer.Antwort(200, PdfMitNullen(10 * 1024), "application/pdf"),
            _ => null,
        };
        var p = Leer();
        Assert.Contains("gross", Nein(Holen(p, s.Basis + "/mit.pdf")).Fehler!);
        Assert.Contains("grösser", Nein(Holen(p, s.Basis + "/ohne.pdf")).Fehler!);
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        Ok(Holen(p, s.Basis + "/klein.pdf"));
    }

    [Fact]
    public void Exe_als_pdf_ausgegeben_wird_nach_Inhalt_abgelehnt()
    {
        s.Weg = pfad => new TestServer.Antwort(200, Exe(), "application/pdf");
        var p = Leer();
        var n = Nein(Holen(p, s.Basis + "/datenblatt.pdf"));
        Assert.Contains("ausführbare Datei", n.Fehler!);
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        Assert.Empty(Directory.EnumerateFiles(Unterlagen.Ordner(p)));
    }

    [Fact]
    public void Erwartete_Art_stimmt_nicht_mit_dem_Inhalt_ueberein()
    {
        s.Weg = _ => new TestServer.Antwort(200, "<html><body><p>Hallo</p></body></html>"u8.ToArray(), "text/html");
        var p = Leer();
        var n = Nein(Holen(p, s.Basis + "/seite", art: "pdf"));
        Assert.Contains("html", n.Fehler!);
        Ok(Holen(p, s.Basis + "/seite", art: "html"));
    }

    [Fact]
    public void Zeitlimit_greift()
    {
        UnterlagenDownload.Zeitlimit = TimeSpan.FromSeconds(1);
        s.Weg = _ => new TestServer.Antwort(200, Pdf(), "application/pdf", Verzoegerung: TimeSpan.FromSeconds(8));
        var p = Leer();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var n = Nein(Holen(p, s.Basis + "/langsam.pdf"));
        Assert.Contains("Zeitlimit", n.Fehler!);
        Assert.True(sw.Elapsed.TotalSeconds < 6, $"{sw.Elapsed.TotalSeconds:0.0} s");
    }

    [Fact]
    public void Anmeldung_und_Fehlerseiten_geben_Vorschlag()
    {
        s.Weg = pfad => pfad == "/geschuetzt.pdf" ? new TestServer.Antwort(401, "Anmeldung"u8.ToArray()) : pfad == "/weg.pdf" ? new TestServer.Antwort(404, "weg"u8.ToArray()) : null;
        var p = Leer();
        Assert.Contains("Anmeldung", Nein(Holen(p, s.Basis + "/geschuetzt.pdf")).Fehler!);
        Assert.Contains("gibt es nicht", Nein(Holen(p, s.Basis + "/weg.pdf")).Fehler!);
    }

    [Fact]
    public void Dieselbe_Datei_zweimal_wird_nicht_doppelt_abgelegt()
    {
        var pdf = Pdf();
        s.Weg = _ => new TestServer.Antwort(200, pdf, "application/pdf");
        var p = Leer();
        Ok(Holen(p, s.Basis + "/a/datenblatt.pdf"));
        var zweit = Ok(Holen(p, s.Basis + "/a/datenblatt.pdf"));
        Assert.Contains(zweit.Hinweise, h => h.Contains("nicht doppelt angelegt"));
        Assert.Equal(1, p.Modell.DerKlasse("Unterlage").Count());
        Assert.Equal(1, Directory.EnumerateFiles(Unterlagen.Ordner(p)).Count(f => !Path.GetFileName(f).StartsWith('.')));
        // dieselbe Datei unter anderer Adresse: eine andere URL ist immer eine eigene Unterlage (Nachvollziehbarkeit)
        Ok(Holen(p, s.Basis + "/b/datenblatt.pdf"));
        var uls = p.Modell.DerKlasse("Unterlage").OrderBy(u => u.Kennzeichen).ToList();
        Assert.Equal(2, uls.Count);
        Assert.Equal([s.Basis + "/a/datenblatt.pdf", s.Basis + "/b/datenblatt.pdf"], uls.Select(u => u.Text("original")).ToArray());
        Assert.NotEqual(uls[0].Text("datei"), uls[1].Text("datei"));
    }

    [Fact]
    public void Gleicher_Dateiname_oder_index_html_unter_verschiedenen_URLs_sind_verschiedene_Unterlagen()
    {
        s.Weg = pfad => new TestServer.Antwort(200, Encoding.UTF8.GetBytes($"<html><body><p>Seite {pfad}</p></body></html>"), "text/html");
        var p = Leer();
        foreach (var u in new[] { "/sg/support/bim", "/uk/solutions/other-residential-products/", "/uk/products/optivent-ultra-ulsa-vav-damper/" })
            Ok(Holen(p, s.Basis + u));
        foreach (var u in new[] { "/x/index.html", "/y/index.html", "/z/index.html", "/z/index.html?lang=de" })
            Ok(Holen(p, s.Basis + u));
        foreach (var u in new[] { "/a/produkt", "/b/produkt", "/c/produkt" }) Ok(Holen(p, s.Basis + u));
        var uls = p.Modell.DerKlasse("Unterlage").ToList();
        Assert.Equal(10, uls.Count);                                            // jede URL eine Unterlage (auch mit Query)
        Assert.Equal(10, uls.Select(u => u.Text("original")).Distinct().Count());
        Assert.Equal(10, uls.Select(u => u.Text("datei")).Distinct().Count());
        // jede nennt ihre eigene Seite
        foreach (var ul in uls)
        {
            var b = UnterlagenService.Lesen(p, UnterlagenService.Aufloesen(p, ul.Kennzeichen)).Befunde;
            Assert.Contains(Art(b, "text"), t => t.Text().StartsWith("Seite ") && ul.Text("original")!.Contains(t.Text()["Seite ".Length..].Split('?')[0]));
        }
        // dieselbe URL zweimal (mit Fragment und mit gleichem Inhalt): wiederverwendet, keine elfte Unterlage
        var wieder = Ok(Holen(p, s.Basis + "/x/index.html#abschnitt"));
        Assert.Contains(wieder.Hinweise, h => h.Contains("nicht doppelt angelegt"));
        Assert.Equal(10, p.Modell.DerKlasse("Unterlage").Count());
    }

    [Fact]
    public void Dieselbe_URL_mit_neuem_Inhalt_ist_eine_neue_Fassung_derselben_Unterlage()
    {
        int stand = 1;
        s.Weg = _ => new TestServer.Antwort(200, Encoding.UTF8.GetBytes($"<html><body><p>Fassung {stand}</p></body></html>"), "text/html");
        var p = Leer();
        Ok(Holen(p, s.Basis + "/seite"));
        var erst = p.Modell.DerKlasse("Unterlage").Single();
        stand = 2;
        var e = Ok(Holen(p, s.Basis + "/seite"));
        var zweit = p.Modell.DerKlasse("Unterlage").Single();
        Assert.Equal(erst.Id, zweit.Id);                                        // dieselbe Unterlage
        Assert.NotEqual(erst.Text("stand"), zweit.Text("stand"));              // neuer Inhalt
        Assert.Contains(e.Hinweise, h => h.Contains("neue Fassung derselben Unterlage"));
        Assert.Equal(s.Basis + "/seite", zweit.Text("original"));
        var b = UnterlagenService.Lesen(p, UnterlagenService.Aufloesen(p, "UL-1")).Befunde;
        Assert.Contains(Art(b, "text"), t => t.Text() == "Fassung 2");
    }

    [Fact]
    public void Identitaet_ohne_Fragment_Host_und_Schema_klein()
    {
        Assert.Equal(UnterlagenDownload.Identitaet(new Uri("https://example.com/a?x=1")), UnterlagenDownload.Identitaet(new Uri("HTTPS://Example.COM:443/a?x=1#frag")));
        Assert.NotEqual(UnterlagenDownload.Identitaet(new Uri("https://example.com/a")), UnterlagenDownload.Identitaet(new Uri("https://example.com/A")));
        Assert.NotEqual(UnterlagenDownload.Identitaet(new Uri("https://example.com/a")), UnterlagenDownload.Identitaet(new Uri("https://example.com/a?x=1")));
        Assert.Equal(UnterlagenDownload.AblageName(new Uri("https://Example.com/a/b.html#x"), null, "html"), UnterlagenDownload.AblageName(new Uri("https://example.com/a/b.html"), null, "html"));
        var lang = UnterlagenDownload.AblageName(new Uri("https://www.flaktgroup.com/uk/products/ventilation-products/residential/optivent-ultra-ulsa-vav-damper/"), null, "html");
        Assert.True(lang.Length < 90 && lang.EndsWith(".html") && lang.Contains("flaktgroup"), lang);
    }

    // ---------- Art nach Inhalt ----------

    [Theory]
    [InlineData("%PDF-1.7\n1 0 obj", "pdf")]
    [InlineData("AC1032xxxxxxxxxx", "dwg")]
    [InlineData("  0\r\nSECTION\r\n  2\r\nHEADER\r\n", "dxf")]
    [InlineData("ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('IFC4'));", "ifc")]
    [InlineData("ISO-10303-21;\nHEADER;\nFILE_SCHEMA(('AUTOMOTIVE_DESIGN'));", "step")]
    [InlineData("solid x\nfacet normal 0 0 1\nouter loop", "stl")]
    [InlineData("v 0 0 0\nv 1 0 0\nv 0 1 0\nf 1 2 3\n", "obj")]
    [InlineData(",Laenge##LENGTH##MILLIMETERS,DN##NUMBER##\nV-15,67,15\n", "typkatalog")]
    [InlineData("Typ;DN;L\nV-15;15;67\nV-20;20;78\n", "csv")]
    [InlineData("<!DOCTYPE html><html><body>x</body></html>", "html")]
    [InlineData("  <html lang=\"de\"><head></head>", "html")]
    public void Art_nach_Inhalt(string inhalt, string erwartet)
    {
        using var o = new Ordner();
        Assert.Equal(erwartet, UnterlagenDownload.ArtErkennen(Datei(o, "x.bin", inhalt), out _));
    }

    [Fact]
    public void Art_nach_Inhalt_Binaer_und_unerlaubt()
    {
        using var o = new Ordner();
        Assert.Equal("png", UnterlagenDownload.ArtErkennen(WriteBytes(o, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0]), out _));
        Assert.Equal("jpg", UnterlagenDownload.ArtErkennen(WriteBytes(o, [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]), out _));
        Assert.Equal("stl", UnterlagenDownload.ArtErkennen(StlBinaer(o), out _));
        Assert.Equal("xlsx", UnterlagenDownload.ArtErkennen(TestXlsx(o), out _));
        Assert.Equal("zip", UnterlagenDownload.ArtErkennen(WriteBytes(o, Zip(("a.pdf", Pdf()))), out _));
        Assert.Null(UnterlagenDownload.ArtErkennen(WriteBytes(o, Exe()), out var g1)); Assert.Contains("ausführbar", g1);
        Assert.Null(UnterlagenDownload.ArtErkennen(WriteBytes(o, Zip(("word/document.xml", "<x/>"u8.ToArray()))), out var g2)); Assert.Contains("Office", g2);
        Assert.Null(UnterlagenDownload.ArtErkennen(WriteBytes(o, [1, 2, 0, 3, 4, 5]), out var g3)); Assert.Contains("Binär", g3);
        Assert.Null(UnterlagenDownload.ArtErkennen(Datei(o, "brief.txt", "Das ist nur ein Brief ohne Tabelle."), out var g4)); Assert.NotEmpty(g4);
        Assert.Null(UnterlagenDownload.ArtErkennen(WriteBytes(o, []), out var g5)); Assert.Contains("leer", g5);
    }

    static string WriteBytes(Ordner o, byte[] b) { var pfad = o.Datei(Guid.NewGuid().ToString("N")[..8] + ".bin"); File.WriteAllBytes(pfad, b); return pfad; }

    // ---------- ZIP ----------

    [Fact]
    public void Zip_mit_PDF_und_STEP_jede_Datei_eine_Unterlage_das_ZIP_nicht()
    {
        var zip = Zip(("R2015/", []), ("R2015/datenblatt.pdf", Pdf()), ("R2015/cad/ventil.stp", Step), ("readme.exe", Exe()), ("R2015/typen.csv", "Typ;DN\nV-15;15\nV-20;20\n"u8.ToArray()));
        s.Weg = pfad => pfad == "/download/r2015.zip" ? new TestServer.Antwort(200, zip, "application/zip") : null;
        var p = Leer();
        var url = s.Basis + "/download/r2015.zip";
        var e = Ok(Holen(p, url));
        var uls = p.Modell.DerKlasse("Unterlage").OrderBy(u => u.Kennzeichen).ToList();
        Assert.Equal(["pdf", "step", "csv"], uls.Select(u => u.Text("art")).ToArray());
        Assert.Equal([$"{url}#R2015/datenblatt.pdf", $"{url}#R2015/cad/ventil.stp", $"{url}#R2015/typen.csv"], uls.Select(u => u.Text("original")).ToArray());
        Assert.All(uls, u => { Assert.Equal("familie", u.Text("zweck")); Assert.NotNull(u.Text("abgerufen")); Assert.Null(u.Geschoss); });
        Assert.DoesNotContain(uls, u => u.Text("art") == "zip");
        Assert.All(uls.Select(u => Unterlagen.OriginalName(u.Text("datei")!)), n => Assert.Matches(@"^127-0-0-1-download-r2015-zip-.+_[0-9a-f]{8}\.[a-z]+$", n));
        Assert.Equal(3, uls.Select(u => u.Text("datei")).Distinct().Count());
        Assert.Contains("datenblatt", Unterlagen.OriginalName(uls[0].Text("datei")!)); Assert.Contains("ventil", Unterlagen.OriginalName(uls[1].Text("datei")!)); Assert.Contains("typen", Unterlagen.OriginalName(uls[2].Text("datei")!));
        Assert.Contains(e.Hinweise, h => h.Contains("3 Dateien abgelegt, 1 übersprungen"));
        Assert.Contains(e.Hinweise, h => h.StartsWith("Übersprungen: «readme.exe»") && h.Contains("ausführbare Datei"));
        Assert.DoesNotContain(Directory.EnumerateFiles(Unterlagen.Ordner(p)), f => f.EndsWith(".zip"));
        var z = JsonNode.Parse(Zusammenfassung(e))!["unterlagen"]!.AsArray();
        Assert.Equal(3, z.Count);
    }

    [Fact]
    public void Zip_mit_Pfad_ausserhalb_des_Ziels_wird_uebersprungen_und_allein_abgelehnt()
    {
        var nurBoese = Zip(("..\\boese.pdf", Pdf()), ("/abs/boese2.pdf", Pdf()), ("C:\\Windows\\x.pdf", Pdf()), ("a/../../b.pdf", Pdf()));
        var gemischt = Zip(("..\\boese.pdf", Pdf()), ("gut.pdf", Pdf()));
        s.Weg = pfad => pfad == "/boese.zip" ? new TestServer.Antwort(200, nurBoese) : pfad == "/gemischt.zip" ? new TestServer.Antwort(200, gemischt) : null;
        var p = Leer();
        var n = Nein(Holen(p, s.Basis + "/boese.zip"));
        Assert.Contains("ausserhalb des Ziels", n.Fehler!);
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        var e = Ok(Holen(p, s.Basis + "/gemischt.zip"));
        Assert.Equal(1, p.Modell.DerKlasse("Unterlage").Count());
        Assert.Contains(e.Hinweise, h => h.StartsWith("Übersprungen: «..\\boese.pdf»") && h.Contains("ausserhalb des Ziels"));
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.GetTempPath(), "boese*.pdf", SearchOption.TopDirectoryOnly), _ => true);
    }

    [Fact]
    public void Zip_Bombe_die_Grenzen_greifen_beim_Entpacken()
    {
        UnterlagenDownload.MaxEintragBytes = 1024 * 1024;
        UnterlagenDownload.MaxEntpacktBytes = 2 * 1024 * 1024;
        // 1. Ein Eintrag, der entpackt viel zu gross ist (Kompressionsrate über 1000): übersprungen, nichts abgelegt
        var bombe = Zip(("gross.pdf", PdfMitNullen(30 * 1024 * 1024)));
        Assert.True(bombe.Length < 100 * 1024, $"Archiv {bombe.Length} Byte");
        // 2. Viele Einträge knapp unter der Eintragsgrenze: die Gesamtgrenze bricht ab
        var summe = Zip(Enumerable.Range(1, 4).Select(i => ($"t{i}.pdf", PdfMitNullen(900 * 1024))).ToArray());
        s.Weg = pfad => pfad == "/bombe.zip" ? new TestServer.Antwort(200, bombe) : pfad == "/summe.zip" ? new TestServer.Antwort(200, summe) : null;
        var p = Leer();
        var n1 = Nein(Holen(p, s.Basis + "/bombe.zip"));
        Assert.Contains("keine Datei einer erlaubten Art", n1.Fehler!);
        Assert.Contains("grösser als 1 MB", n1.Fehler!);
        var n2 = Nein(Holen(p, s.Basis + "/summe.zip"));
        Assert.Contains("Zip-Bombe", n2.Fehler!);
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
        Assert.Empty(Directory.EnumerateFiles(Unterlagen.Ordner(p)).Where(f => !Path.GetFileName(f).StartsWith('.')));
    }

    [Fact]
    public void Zip_zu_viele_Eintraege()
    {
        UnterlagenDownload.MaxEintraege = 5;
        var viele = Zip(Enumerable.Range(1, 8).Select(i => ($"d{i}.pdf", Pdf())).ToArray());
        s.Weg = _ => new TestServer.Antwort(200, viele);
        var p = Leer();
        var n = Nein(Holen(p, s.Basis + "/viele.zip"));
        Assert.Contains("mehr als 5 Einträge", n.Fehler!);
        Assert.Empty(p.Modell.DerKlasse("Unterlage"));
    }

    [Fact]
    public void Zip_verschachtelt_hoechstens_eine_Ebene()
    {
        var innen = Zip(("tief.pdf", Pdf()));
        var mitte = Zip(("mitte.pdf", Pdf("m.pdf")), ("innen.zip", innen));
        var aussen = Zip(("oben.step", Step), ("mitte.zip", mitte));
        var ok = Zip(("oben.step", Step), ("einmal.zip", Zip(("zwei.pdf", Pdf()))));
        s.Weg = pfad => pfad == "/zwei.zip" ? new TestServer.Antwort(200, aussen) : pfad == "/eins.zip" ? new TestServer.Antwort(200, ok) : null;
        var p = Leer();
        var e = Ok(Holen(p, s.Basis + "/zwei.zip"));
        Assert.Equal(["step", "pdf"], p.Modell.DerKlasse("Unterlage").OrderBy(u => u.Kennzeichen).Select(u => u.Text("art")).ToArray());   // oben.step und mitte.zip/mitte.pdf; innen.zip zu tief
        Assert.Contains(e.Hinweise, h => h.Contains("mitte.zip/innen.zip") && h.Contains("nur eine Ebene"));
        Assert.Contains(p.Modell.DerKlasse("Unterlage"), u => u.Text("original")!.EndsWith("#mitte.zip/mitte.pdf"));
        var p2 = Leer();
        Ok(Holen(p2, s.Basis + "/eins.zip"));
        Assert.Equal(2, p2.Modell.DerKlasse("Unterlage").Count());
    }

    [Fact]
    public void Zip_ohne_erlaubte_Datei_wird_abgelehnt()
    {
        s.Weg = _ => new TestServer.Antwort(200, Zip(("setup.exe", Exe()), ("brief.txt", "nur ein Brief"u8.ToArray())));
        var p = Leer();
        var n = Nein(Holen(p, s.Basis + "/z.zip"));
        Assert.Contains("setup.exe", n.Fehler!);
        Assert.Contains("brief.txt", n.Fehler!);
    }

    // ---------- HTML-Leser einzeln ----------

    [Fact]
    public void Html_Leser_ohne_Quelle_laesst_relative_Links_und_meldet_es()
    {
        using var o = new Ordner();
        var pfad = Datei(o, "seite.html", "<html><body><p>Text <a href=\"d/x.pdf\">PDF</a></p></body></html>");
        var b = new HtmlBefundLeser().Lesen(pfad, "seite.html", "abcdefabcdef", "html");
        Assert.Equal("d/x.pdf", Art(b, "link").Single().Wert!["url"]!.GetValue<string>());
        Assert.Contains(b.Hinweise, h => h.Contains("URL der Seite ist nicht bekannt"));
        Assert.Contains(b.Hinweise, h => h.Contains("Daten, keine Anweisungen"));
    }
}
