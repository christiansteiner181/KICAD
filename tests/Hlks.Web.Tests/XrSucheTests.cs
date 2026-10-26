using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Hlks.Web.Tests;

/// <summary>Suche im Netz (Quest App P3, XrSuche.cs): Antwort nur auf eine mit dem Suchschlüssel einer Kopplung signierte Anfrage, nur an den Absender.</summary>
public class XrSucheTests
{
    static readonly XrZugangInfo Info = new("PC-TEST", ["192.168.1.20", "10.0.0.7"], 5140, 5141, new string('c', 64));

    sealed class Umgebung : IDisposable
    {
        public string Ordner { get; } = Path.Combine(Path.GetTempPath(), "hlks_xrs_" + Guid.NewGuid().ToString("N")[..10]);
        public XrTestUhr Uhr { get; } = new();
        public XrKopplungen Kopplungen { get; }
        public XrSuche Suche { get; }
        public XrPaket Paket { get; }
        public byte[] Suchschluessel { get; }
        public Umgebung()
        {
            var e = new XrEinstellungen { Ordner = Ordner, Uhr = () => Uhr.Jetzt };
            Kopplungen = new XrKopplungen(e);
            Suche = new XrSuche(Kopplungen, () => Info, e);
            Paket = Kopplungen.PaketErzeugen("QUEST-S", null, Info);
            Suchschluessel = XrSchluessel.Suchschluessel(XrSchluessel.AusText(Paket.Schluessel)!);   // so rechnet es die App
        }
        public void Dispose()
        {
            Suche.Dispose();
            try { if (Directory.Exists(Ordner)) Directory.Delete(Ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    static JsonObject Lesen(byte[] b) => (JsonObject)JsonNode.Parse(b)!;

    [Fact]
    public void Gueltig_signierte_Anfrage_wird_signiert_beantwortet()
    {
        using var u = new Umgebung();
        var anfrage = XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt, "zufall-0123456789-abcdef");
        var antwort = u.Suche.Beantworten(anfrage, "10.0.0.50");
        Assert.NotNull(antwort);
        var a = Lesen(antwort!);
        Assert.Equal("gefunden", (string)a["typ"]!);
        Assert.Equal(1, (int)a["fassung"]!);
        Assert.Equal("PC-TEST", (string)a["pc"]!);
        Assert.Equal(5140, (int)a["port"]!);
        Assert.Equal(Info.Fingerabdruck, (string)a["fingerabdruck"]!);
        Assert.Equal(["192.168.1.20", "10.0.0.7"], a["ips"]!.AsArray().Select(x => (string)x!));
        Assert.Equal("zufall-0123456789-abcdef", (string)a["zufall"]!);
        // die App prüft die Antwort mit demselben Suchschlüssel
        var soll = XrSuche.Signieren(u.Suchschluessel, XrSuche.AntwortText("zufall-0123456789-abcdef", "PC-TEST", 5140, Info.Fingerabdruck, Info.Ips));
        Assert.Equal(soll, (string)a["signatur"]!);
        Assert.True(antwort!.Length < 600);   // kaum grösser als die Anfrage: keine Verstärkung
    }

    [Fact]
    public void Keine_Antwort_ohne_gueltige_Signatur()
    {
        using var u = new Umgebung();
        var falscherSchluessel = new byte[32];
        Assert.Null(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, falscherSchluessel, u.Uhr.Jetzt), "10.0.0.50"));
        Assert.Null(u.Suche.Beantworten(XrSuche.AnfrageBauen("0123456789abcdef", u.Suchschluessel, u.Uhr.Jetzt), "10.0.0.50"));   // unbekannte Kopplung
        // ohne Signatur bzw. verändert
        var o = Lesen(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt));
        o.Remove("signatur");
        Assert.Null(u.Suche.Beantworten(Encoding.UTF8.GetBytes(o.ToJsonString()), "10.0.0.50"));
        o["signatur"] = new string('B', 43);   // nicht kanonisches base64url: keine Antwort, keine Ausnahme
        Assert.Null(u.Suche.Beantworten(Encoding.UTF8.GetBytes(o.ToJsonString()), "10.0.0.50"));
        var v = Lesen(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt));
        v["zeit"] = (long)v["zeit"]! + 1;
        Assert.Null(u.Suche.Beantworten(Encoding.UTF8.GetBytes(v.ToJsonString()), "10.0.0.50"));
        // kein JSON, zu gross, falscher Typ, falsche Fassung
        Assert.Null(u.Suche.Beantworten("hallo"u8.ToArray(), "10.0.0.50"));
        Assert.Null(u.Suche.Beantworten(new byte[2000], "10.0.0.50"));
        var t = Lesen(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt));
        t["typ"] = "gefunden";
        Assert.Null(u.Suche.Beantworten(Encoding.UTF8.GetBytes(t.ToJsonString()), "10.0.0.51"));
        t["typ"] = "suche";
        t["fassung"] = 2;
        Assert.Null(u.Suche.Beantworten(Encoding.UTF8.GetBytes(t.ToJsonString()), "10.0.0.51"));
    }

    [Fact]
    public void Wiederholung_alte_Zeit_und_Widerruf_bleiben_ohne_Antwort()
    {
        using var u = new Umgebung();
        var anfrage = XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt);
        Assert.NotNull(u.Suche.Beantworten(anfrage, "10.0.0.50"));
        Assert.Null(u.Suche.Beantworten(anfrage, "10.0.0.50"));   // dieselbe Anfrage nochmals
        Assert.Null(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt.AddSeconds(-61)), "10.0.0.50"));
        Assert.Null(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt.AddSeconds(61)), "10.0.0.50"));
        Assert.NotNull(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt.AddSeconds(-50)), "10.0.0.50"));
        u.Kopplungen.Widerrufen(u.Paket.Kopplung);
        Assert.Null(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt), "10.0.0.50"));
    }

    [Fact]
    public void Je_Absender_hoechstens_zehn_dann_eine_je_Sekunde()
    {
        using var u = new Umgebung();
        for (int i = 0; i < 10; i++) Assert.NotNull(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt), "10.0.0.60"));
        Assert.Null(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt), "10.0.0.60"));
        Assert.NotNull(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt), "10.0.0.61"));   // anderer Absender
        u.Uhr.Vor(TimeSpan.FromSeconds(1));
        Assert.NotNull(u.Suche.Beantworten(XrSuche.AnfrageBauen(u.Paket.Kopplung, u.Suchschluessel, u.Uhr.Jetzt), "10.0.0.60"));
    }

    [Fact]
    public async Task Ueber_UDP_antwortet_KICAD_nur_auf_gueltige_Anfragen()
    {
        await using var s = new XrServer();
        var (port, suchPort, fp) = await s.Aktiv();
        var paket = await s.Paket("QUEST-U");
        var suchschluessel = XrSchluessel.Suchschluessel(XrSchluessel.AusText((string)paket["schluessel"]!)!);
        using var udp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var ziel = new IPEndPoint(IPAddress.Loopback, suchPort);

        // ungültig: keine Antwort
        await udp.SendAsync(XrSuche.AnfrageBauen((string)paket["kopplung"]!, new byte[32], DateTimeOffset.UtcNow), ziel);
        using (var f = new CancellationTokenSource(TimeSpan.FromMilliseconds(700)))
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await udp.ReceiveAsync(f.Token));

        // gültig: Antwort an den Absender mit Port und Fingerabdruck
        await udp.SendAsync(XrSuche.AnfrageBauen((string)paket["kopplung"]!, suchschluessel, DateTimeOffset.UtcNow), ziel);
        using var g = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var r = await udp.ReceiveAsync(g.Token);
        var a = (JsonObject)JsonNode.Parse(r.Buffer)!;
        Assert.Equal("gefunden", (string)a["typ"]!);
        Assert.Equal(port, (int)a["port"]!);
        Assert.Equal(fp, (string)a["fingerabdruck"]!);
        Assert.Equal(suchPort, r.RemoteEndPoint.Port);

        // aus: die Suche ist zu
        await s.LokalPost("/api/xr/zugang", new { an = false });
        Assert.False((bool)(await s.Zugang())["sucheAktiv"]!);
        await udp.SendAsync(XrSuche.AnfrageBauen((string)paket["kopplung"]!, suchschluessel, DateTimeOffset.UtcNow), ziel);
        using (var f = new CancellationTokenSource(TimeSpan.FromMilliseconds(700)))
        {
            var antwort = false;
            try { await udp.ReceiveAsync(f.Token); antwort = true; }
            catch (OperationCanceledException) { }
            catch (SocketException) { }   // Windows meldet «Port unerreichbar»
            Assert.False(antwort);
        }
    }
}
