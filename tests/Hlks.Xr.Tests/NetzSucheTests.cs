// Quest App P4a: den PC finden (PcSuche) – Anfrage und Prüfung der Antwort nach dem Protokoll von XrSuche.cs, Broadcast-Adressen, Suche über UDP auf
// 127.0.0.1 mit einem Antworter nach dem Protokoll, Reihenfolge gespeicherte Adressen → Paket → Suche. Die Gegenprobe mit dem echten Server steht im
// Integrationstest (Hlks.Web.Tests, XrAppKlientTests).

using System.Buffers.Text;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Tests;

public class NetzSucheTests
{
    static string Signieren(byte[] schluessel, string text) => Base64Url.EncodeToString(HMACSHA256.HashData(schluessel, Encoding.UTF8.GetBytes(text)));

    static byte[] AntwortBauen(Kopplungspaket p, string zufall, string pc = "PC-TEST", int port = 5140, string? fp = null, string[]? ips = null, byte[]? schluessel = null)
    {
        fp ??= p.Fingerabdruck;
        ips ??= ["192.168.1.20"];
        var o = new JsonObject
        {
            ["typ"] = "gefunden", ["fassung"] = 1, ["pc"] = pc, ["ips"] = new JsonArray([.. ips.Select(i => (JsonNode)JsonValue.Create(i))]), ["port"] = port,
            ["fingerabdruck"] = fp, ["zufall"] = zufall,
            ["signatur"] = Signieren(schluessel ?? p.Suchschluessel(), $"hlks-xr-gefunden|1|{zufall}|{pc}|{port}|{fp}|{string.Join(",", ips)}"),
        };
        return Encoding.UTF8.GetBytes(o.ToJsonString());
    }

    /// <summary>Antwortet auf UDP wie KICAD (XrSuche.cs): nur auf gültig signierte Anfragen, an den Absender.</summary>
    sealed class Antworter : IDisposable
    {
        readonly UdpClient udp = new(new IPEndPoint(IPAddress.Loopback, 0));
        readonly CancellationTokenSource stopp = new();
        public int Port => ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        public int Anfragen;

        public Antworter(Kopplungspaket p, Func<string, byte[]?> antwort)
        {
            if (OperatingSystem.IsWindows()) udp.Client.IOControl(-1744830452, [0, 0, 0, 0], null);
            _ = Task.Run(async () =>
            {
                while (!stopp.IsCancellationRequested)
                {
                    UdpReceiveResult r;
                    try { r = await udp.ReceiveAsync(stopp.Token); } catch (Exception) { return; }
                    Interlocked.Increment(ref Anfragen);
                    var o = (JsonObject)JsonNode.Parse(r.Buffer)!;
                    var zufall = (string)o["zufall"]!;
                    var text = $"hlks-xr-suche|1|{(string)o["kopplung"]!}|{(long)o["zeit"]!}|{zufall}";
                    if ((string)o["typ"]! != "suche" || (string)o["signatur"]! != Signieren(p.Suchschluessel(), text)) continue;
                    if (antwort(zufall) is { } a) await udp.SendAsync(a, r.RemoteEndPoint);
                }
            });
        }

        public IPEndPoint Ziel => new(IPAddress.Loopback, Port);
        public void Dispose() { stopp.Cancel(); udp.Dispose(); }
    }

    /// <summary>Probe ohne Netz: Antwort je Adresse (Host:Port) aus einer Tabelle, sonst keine Verbindung.</summary>
    sealed class ProbeHandler(Dictionary<string, HttpStatusCode> antworten) : HttpMessageHandler
    {
        public List<string> Gefragt { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken abbruch)
        {
            var wo = $"{r.RequestUri!.Host}:{r.RequestUri.Port}";
            lock (Gefragt) Gefragt.Add(wo);
            Assert.Equal("/api/xr/verbindung", r.RequestUri.AbsolutePath);
            Assert.Equal("Bearer", r.Headers.Authorization?.Scheme);
            if (!antworten.TryGetValue(wo, out var s)) throw new HttpRequestException("keine Verbindung");
            var body = s == HttpStatusCode.OK ? "{\"ok\":true,\"fassung\":1,\"pc\":\"PC-ECHT\"}" : "{\"fehler\":\"Schlüssel fehlt oder gilt nicht.\"}";
            return Task.FromResult(new HttpResponseMessage(s) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    static SucheOptionen Mit(ProbeHandler h, Kopplungspaket p, IReadOnlyList<IPEndPoint>? ziele = null, bool suchen = true) => new()
    {
        ZugangFabrik = a => new PcZugang(a, p, h), Ziele = ziele ?? [], Suchen = suchen, SucheDauer = TimeSpan.FromSeconds(1.5), ProbeFrist = TimeSpan.FromSeconds(2),
    };

    // ---------------------------------------------------------------------------------------------- Anfrage und Antwort

    [Fact]
    public void Anfrage_ist_signiert_wie_XrSuche_es_prueft()
    {
        var p = NetzHilfen.Paket();
        var zeit = new DateTimeOffset(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);
        var zufall = PcSuche.NeuerZufall();
        Assert.Matches("^[A-Za-z0-9_-]{22}$", zufall);
        var o = (JsonObject)JsonNode.Parse(PcSuche.Anfrage(p, zeit, zufall))!;
        Assert.Equal("suche", (string)o["typ"]!);
        Assert.Equal(1, (int)o["fassung"]!);
        Assert.Equal(p.Kopplung, (string)o["kopplung"]!);
        Assert.Equal(zeit.ToUnixTimeMilliseconds(), (long)o["zeit"]!);
        Assert.Equal(zufall, (string)o["zufall"]!);
        Assert.Equal(Signieren(p.Suchschluessel(), $"hlks-xr-suche|1|{p.Kopplung}|{zeit.ToUnixTimeMilliseconds()}|{zufall}"), (string)o["signatur"]!);
        Assert.DoesNotContain(p.Schluessel, Encoding.UTF8.GetString(PcSuche.Anfrage(p, zeit, zufall)));   // nie der Schlüssel selbst
        Assert.True(PcSuche.Anfrage(p, zeit, zufall).Length < 1024);   // XrSuche.DatagrammMax
    }

    [Fact]
    public void Antwort_gilt_nur_mit_eigenem_Zufall_und_gueltiger_Signatur()
    {
        var p = NetzHilfen.Paket();
        var a = PcSuche.AntwortPruefen(AntwortBauen(p, "zufall-1", ips: ["192.168.1.20", "10.0.0.9", "fe80::1"]), p, ["zufall-1"], "192.168.1.20")!;
        Assert.Equal("PC-TEST", a.Pc);
        Assert.Equal(["192.168.1.20", "10.0.0.9"], a.Ips);   // nur IPv4
        Assert.Equal(5140, a.Port);
        Assert.True(a.FingerabdruckPasst);
        Assert.Equal("192.168.1.20", a.Absender);
        Assert.Null(PcSuche.AntwortPruefen(AntwortBauen(p, "zufall-1"), p, ["zufall-2"]));                         // fremder Zufall (Wiederholung)
        Assert.Null(PcSuche.AntwortPruefen(AntwortBauen(p, "z", schluessel: new byte[32]), p, ["z"]));             // falscher Schlüssel
        var echt = (JsonObject)JsonNode.Parse(AntwortBauen(p, "z"))!;
        echt["ips"] = new JsonArray("6.6.6.6");                                                                   // Adressen verändert
        Assert.Null(PcSuche.AntwortPruefen(Encoding.UTF8.GetBytes(echt.ToJsonString()), p, ["z"]));
        var anders = PcSuche.AntwortPruefen(AntwortBauen(p, "z", fp: new string('b', 64)), p, ["z"])!;
        Assert.False(anders.FingerabdruckPasst);                                                                  // gültig signiert, neues Zertifikat
        Assert.Null(PcSuche.AntwortPruefen("kein json"u8, p, ["z"]));
        Assert.Null(PcSuche.AntwortPruefen(new byte[5000], p, ["z"]));
        Assert.Null(PcSuche.AntwortPruefen(Encoding.UTF8.GetBytes("{\"typ\":\"suche\",\"fassung\":1}"), p, ["z"]));
    }

    [Fact]
    public void Broadcast_Adressen_aus_Adresse_und_Maske()
    {
        Assert.Equal(IPAddress.Parse("192.168.1.255"), PcSuche.Broadcast(IPAddress.Parse("192.168.1.20"), IPAddress.Parse("255.255.255.0")));
        Assert.Equal(IPAddress.Parse("10.0.255.255"), PcSuche.Broadcast(IPAddress.Parse("10.0.5.7"), IPAddress.Parse("255.255.0.0")));
        Assert.Equal(IPAddress.Parse("172.16.7.255"), PcSuche.Broadcast(IPAddress.Parse("172.16.4.1"), IPAddress.Parse("255.255.252.0")));
        Assert.Null(PcSuche.Broadcast(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("255.255.255.255")));
        Assert.Null(PcSuche.Broadcast(IPAddress.Parse("10.0.0.1"), IPAddress.Parse("255.255.255.254")));
        Assert.Null(PcSuche.Broadcast(IPAddress.Parse("10.0.0.1"), null));
        var ziele = PcSuche.BroadcastZiele(5141);
        Assert.Equal(new IPEndPoint(IPAddress.Broadcast, 5141), ziele[0]);
        Assert.All(ziele, z => Assert.Equal(5141, z.Port));
        Assert.DoesNotContain(ziele, z => IPAddress.IsLoopback(z.Address));
    }

    // ---------------------------------------------------------------------------------------------- Suche über UDP

    [Fact]
    public async Task Rufen_findet_den_Antworter_und_verwirft_falsche_Antworten()
    {
        var p = NetzHilfen.Paket();
        using (var gut = new Antworter(p, z => AntwortBauen(p, z, ips: ["127.0.0.1"], port: 6140)))
        {
            var a = await PcSuche.Rufen(p, [gut.Ziel], new SucheOptionen { SucheDauer = TimeSpan.FromSeconds(3) });
            Assert.NotNull(a);
            Assert.Equal(6140, a!.Port);
            Assert.Equal("127.0.0.1", a.Absender);
        }
        using var falsch = new Antworter(p, z => AntwortBauen(p, z, schluessel: new byte[32]));
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        Assert.Null(await PcSuche.Rufen(p, [falsch.Ziel], new SucheOptionen { SucheDauer = TimeSpan.FromSeconds(0.8) }));
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(3), $"{uhr.Elapsed}");
        Assert.Equal(2, falsch.Anfragen);   // zweite Anfrage nach der halben Dauer (UDP kann verloren gehen)
    }

    // ---------------------------------------------------------------------------------------------- Finden

    [Fact]
    public async Task Finden_gespeicherte_Adresse_zuerst_dann_Paket()
    {
        var p = NetzHilfen.Paket();
        var h = new ProbeHandler(new() { ["10.1.1.1:6000"] = HttpStatusCode.OK, ["192.168.1.20:5140"] = HttpStatusCode.OK });
        var f = await PcSuche.Finden(p, [("10.1.1.1", 6000)], Mit(h, p, suchen: false));
        Assert.Equal(PcFundZustand.Gefunden, f.Zustand);
        Assert.True(f.Adresse!.Quelle is "gespeichert" or "paket");   // gleichzeitig probiert, die erste Antwort gilt
        Assert.Equal("PC-ECHT", f.Adresse.Pc);                         // Name aus der Probe

        var h2 = new ProbeHandler(new() { ["10.0.0.5:5140"] = HttpStatusCode.OK });
        var g = await PcSuche.Finden(p, [("10.1.1.1", 6000)], Mit(h2, p, suchen: false));
        Assert.Equal(new PcAdresse("10.0.0.5", 5140, "PC-ECHT", "paket"), g.Adresse);
        Assert.Equal(["10.0.0.5:5140", "10.1.1.1:6000", "192.168.1.20:5140"], h2.Gefragt.Order());
        Assert.Equal(new Uri("wss://10.0.0.5:5140/api/xr/kanal"), g.Adresse!.Kanal);
    }

    [Fact]
    public async Task Finden_401_heisst_neu_koppeln_ohne_Antwort_nicht_gefunden()
    {
        var p = NetzHilfen.Paket();
        var h = new ProbeHandler(new() { ["192.168.1.20:5140"] = HttpStatusCode.Unauthorized });
        var f = await PcSuche.Finden(p, null, Mit(h, p));
        Assert.Equal(PcFundZustand.SchluesselUngueltig, f.Zustand);
        Assert.Contains("neu koppeln", f.Meldung);
        Assert.Null(f.Adresse);
        var g = await PcSuche.Finden(p, null, Mit(new ProbeHandler(new()), p, suchen: false));
        Assert.Equal(PcFundZustand.NichtGefunden, g.Zustand);
    }

    [Fact]
    public async Task Finden_ueber_die_Suche_mit_Absender_und_neuem_Port()
    {
        var p = NetzHilfen.Paket();
        using var antworter = new Antworter(p, z => AntwortBauen(p, z, ips: ["192.168.7.7"], port: 6140));
        var h = new ProbeHandler(new() { ["127.0.0.1:6140"] = HttpStatusCode.OK });
        var f = await PcSuche.Finden(p, null, Mit(h, p, [antworter.Ziel]));
        Assert.Equal(PcFundZustand.Gefunden, f.Zustand);
        Assert.Equal(new PcAdresse("127.0.0.1", 6140, "PC-ECHT", "suche"), f.Adresse);

        using var neu = new Antworter(p, z => AntwortBauen(p, z, fp: new string('c', 64)));
        var g = await PcSuche.Finden(p, null, Mit(new ProbeHandler(new()), p, [neu.Ziel]));
        Assert.Equal(PcFundZustand.ZertifikatAnders, g.Zustand);

        using var stumm = new Antworter(p, _ => null);
        var s = await PcSuche.Finden(p, null, Mit(new ProbeHandler(new()), p, [stumm.Ziel]));
        Assert.Equal(PcFundZustand.NichtGefunden, s.Zustand);
        Assert.Contains("Suche ohne Antwort", s.Meldung);
    }
}
