using System.Net;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Hlks.Xr;
using Hlks.Xr.Modell;
using Hlks.Xr.Netz;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Hlks.Web.Tests;

/// <summary>
/// Quest App P4a: der Netz-Teil der App (src/Hlks.Xr/Netz) gegen den echten Netzzugang von KICAD (XrZugang.cs) – auf 127.0.0.1 mit freien Ports, echtem TLS
/// und gepinntem Fingerabdruck (XrServer). Kopplungspaket wie von POST /api/xr/kopplung/paket, PC finden (Adressen des Pakets und Suche über UDP), Modell
/// laden mit Zwischenspeicher (200, dann 304, dann ohne PC), Szene und Start aus den GLB des Beispielprojekts, Live-Kanal mit Pose, Auftrag, Stellschraube,
/// Wiederverbinden und Widerruf (4001) bzw. Ersetzen (4002).
/// Solange der Netzzugang die GLB-Endpunkte (P2) noch nicht freigibt (paralleler Nachtrag P3), läuft der Modellteil über localhost; die Testausgabe nennt
/// den Weg («Modell über …»).
/// </summary>
public class XrAppKlientTests(ITestOutputHelper aus)
{
    static async Task Warte(Func<Task<bool>> bedingung, string was, double sekunden = 10)
    {
        var bis = DateTime.UtcNow.AddSeconds(sekunden);
        while (!await bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException(was);
            await Task.Delay(30);
        }
    }

    static async Task<JsonObject> Teilnehmer(XrServer s) => (JsonObject)JsonNode.Parse(await s.Lokal.GetStringAsync("/api/xr/teilnehmer"))!;

    static async Task ProjektAnlegen(XrServer s)
    {
        var ordner = Path.Combine(s.Ordner, "projekt", "c_Pläne und Modelle", "e_KICAD");
        var (code, j) = await s.LokalPost("/api/projekt/anlegen", new { ordner, name = "MFH Musterweg", beispiel = true });
        Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
    }

    /// <summary>Netzzugang, wenn er die GLB-Endpunkte kennt; sonst localhost (siehe Klassenkommentar).</summary>
    async Task<IModellQuelle> ModellQuelle(XrServer s, PcZugang zugang)
    {
        try
        {
            await zugang.Modell.GeschosseText(CancellationToken.None);
            aus.WriteLine("Modell über den Netzzugang (HTTPS, Pinning, Schlüssel).");
            return zugang.Modell;
        }
        catch (XrNetzFehler f) when (f.Message.Contains("(404)", StringComparison.Ordinal))
        {
            aus.WriteLine("Modell über localhost: der Netzzugang gibt /api/xr/geschosse und /api/xr/geschoss/{id}.glb noch nicht frei (404).");
            return new ModellHttp(s.Lokal, "lokal");
        }
    }

    [Fact]
    public async Task App_koppelt_findet_den_PC_laedt_das_Modell_und_spricht_im_Kanal()
    {
        await using var s = new XrServer();
        var (port, suchPort, fp) = await s.Aktiv();
        await ProjektAnlegen(s);

        // ---------------------------------------------------------------- Kopplungspaket (Weg A)
        var roh = await s.Paket("QUEST-P4A", "Quest Test");
        var paket = Kopplungspaket.Lesen(new JsonObject { ["paket"] = roh.DeepClone() }.ToJsonString());
        Assert.Equal(port, paket.Port);
        Assert.Equal(suchPort, paket.SuchPort);
        Assert.Equal(fp, paket.Fingerabdruck);
        Assert.Equal(["127.0.0.1"], paket.Ips);
        Assert.Equal(XrServer.Token(roh), paket.Token);

        // ---------------------------------------------------------------- PC finden
        var f = await PcSuche.Finden(paket, null, new SucheOptionen { Suchen = false });
        Assert.True(f.Zustand == PcFundZustand.Gefunden, f.Meldung);
        Assert.Equal(new PcAdresse("127.0.0.1", port, Environment.MachineName, "paket"), f.Adresse);
        // ohne Adressen im Paket: Suche über UDP am Suchport (hier direkt an 127.0.0.1 statt Broadcast), Antwort mit HMAC geprüft
        var g = await PcSuche.Finden(paket with { Ips = [] }, null, new SucheOptionen { Ziele = [new IPEndPoint(IPAddress.Loopback, suchPort)] });
        Assert.True(g.Zustand == PcFundZustand.Gefunden, g.Meldung);
        Assert.Equal("suche", g.Adresse!.Quelle);
        Assert.Equal(port, g.Adresse.Port);
        // anderer Pin: das Zertifikat von KICAD wird abgelehnt, keine Verbindung
        var falsch = await PcSuche.Finden(paket with { Fingerabdruck = new string('0', 64) }, null, new SucheOptionen { Suchen = false, ProbeFrist = TimeSpan.FromSeconds(5) });
        Assert.Equal(PcFundZustand.NichtGefunden, falsch.Zustand);

        // ---------------------------------------------------------------- Modell: laden, 304, ohne PC
        using var zugang = new PcZugang(f.Adresse!, paket);
        Assert.True((await zugang.Probe(CancellationToken.None)).Ok);
        var quelle = await ModellQuelle(s, zugang);
        var sp = new GlbZwischenspeicher(Path.Combine(s.Ordner, "app"));
        var stand = await ModellLader.VomPc(quelle, sp, new ModellLadeOptionen { Pc = paket.Pc });
        Assert.Equal(["UG modell", "EG modell", "1.OG modell"], stand.Teile.Select(t => $"{t.Geschoss.Kennzeichen} {t.Teil}"));   // das Beispiel hat keine Unterlage
        Assert.All(stand.Teile, t => Assert.Equal(GlbQuelle.Geladen, t.Quelle));
        Assert.Empty(stand.Hinweise);
        var bedingt = await ModellLader.VomPc(quelle, sp, new ModellLadeOptionen { Bedingt = true, Pc = paket.Pc });
        Assert.All(bedingt.Teile, t => Assert.Equal(GlbQuelle.Unveraendert, t.Quelle));   // If-None-Match → 304
        var ohnePc = ModellLader.AusZwischenspeicher(sp)!;
        Assert.Equal(ModellStand.AusZwischenspeicher, ohnePc.Quelle);
        Assert.Equal(stand.Teile.Select(t => t.Glb), ohnePc.Teile.Select(t => t.Glb));

        // ---------------------------------------------------------------- Szene und Start aus den GLB von KICAD
        var szene = ModellSzene.AusGlb(stand.Teile.Select(t => (t.Glb, t.Geschoss.Id, t.Teil, $"{t.Geschoss.Kennzeichen} {t.Teil}")));
        Assert.Empty(szene.Hinweise);
        Assert.NotEmpty(szene.Raeume);
        Assert.NotEmpty(szene.DurchgangsBauteile);
        Assert.Equal(stand.Liste.Bezugspunkt, szene.Bezugspunkt);
        var (netze, dreiecke) = szene.Sichtbar(new HashSet<string> { ModellSzene.GruppeRaum });
        aus.WriteLine($"Beispielprojekt: {szene.Netze.Count} Netze ({netze} sichtbar ohne Räume), {dreiecke} Dreiecke, {szene.Elemente.Count} Elemente, " +
            $"{szene.Raeume.Count} Räume, {szene.DurchgangsBauteile.Count} Bauteile im Durchgang");
        var start = await AppStart.Waehlen(null, null, szene, stand.Liste);
        Assert.Equal(Startquellen.Eg, start.Punkt.Quelle);
        var eg = stand.Liste.Geschosse.Single(x => x.Kennzeichen == "EG");
        Assert.Equal(eg.Id, szene.Finde(start.Punkt.Standort!.Raum!)!.GeschossId);
        var verfolger = new DurchgangVerfolger(szene);
        var wand = szene.DurchgangsBauteile.First(e => e.Art == "wand");
        verfolger.Pruefen(Vec3.Mitte(wand.Box.Min, wand.Box.Max));
        Assert.Contains(wand.Schluessel, verfolger.Nah);

        // ---------------------------------------------------------------- Live-Kanal
        var willkommen = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var auftraege = Channel.CreateUnbounded<JsonElement>();
        var schrauben = Channel.CreateUnbounded<(string, JsonElement)>();
        int willkommenAnzahl = 0;
        await using var kanal = new KanalVerbindung(paket, _ => Task.FromResult<Uri?>(f.Adresse!.Kanal),
            new KanalOptionen { Name = "Quest Test", Geraet = "Meta Quest 3", App = "KICAD P4a", Wartezeit = _ => TimeSpan.FromMilliseconds(200) });
        kanal.Willkommen += w => { Interlocked.Increment(ref willkommenAnzahl); willkommen.TrySetResult(w.Clone()); };
        kanal.Auftrag += a => auftraege.Writer.TryWrite(a.Clone());
        kanal.Stellschraube += (k, w) => schrauben.Writer.TryWrite((k, w.Clone()));
        kanal.Starten();
        var w = await willkommen.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Matches("^t[0-9a-f]{8}$", w.GetProperty("id").GetString());
        Assert.Equal(KanalZustand.Verbunden, kanal.Zustand);

        // Pose: Kopf in der Szene der App (m, y oben) → KICAD (mm, z oben); der PC sieht sie in GET /api/xr/teilnehmer
        var kopf = new Hlks.Xr.Vec3(2.0, 1.7, -1.5);
        var blick = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);   // nach Westen gedreht
        var hand = new KicadHand(new KicadLage(KicadUmrechnung.PunktZuKicad(new Hlks.Xr.Vec3(2.2, 1.2, -1.9), szene.Bezugspunkt), [0, 0, 0, 1]),
            KicadUmrechnung.PunktZuKicad(new Hlks.Xr.Vec3(2.2, 1.15, -2.0), szene.Bezugspunkt), true, 0.9);
        kanal.PoseSetzen(new KicadPose(new KicadLage(KicadUmrechnung.PunktZuKicad(kopf, szene.Bezugspunkt), KicadUmrechnung.DrehungZuKicad(blick)), null, hand));
        await Warte(async () => (await Teilnehmer(s))["teilnehmer"]!.AsArray().FirstOrDefault()?["pose"] is not null, "die Pose kommt nicht an");
        var t = (await Teilnehmer(s))["teilnehmer"]![0]!;
        Assert.Equal("Quest Test", (string)t["name"]!);
        Assert.Equal("Meta Quest 3", (string)t["geraet"]!);
        Assert.Equal("KICAD P4a", (string)t["app"]!);
        Assert.Equal("QUEST-P4A", (string)t["brille"]!);
        Assert.Equal([2000.0, 1500, 1700], t["pose"]!["kopf"]!["pos"]!.AsArray().Select(x => (double)x!));
        Assert.InRange((double)t["pose"]!["kopf"]!["rot"]![2]!, 0.706, 0.708);   // Drehung um KICAD-z (oben); der Server rundet auf 3 Stellen
        Assert.True((bool)t["pose"]!["rechts"]!["pinch"]!);
        Assert.Null(t["pose"]!["links"]);
        Assert.Equal("begehen", (string)t["pose"]!["modus"]!);
        Assert.True(kanal.PosenGesendet >= 1);

        // Auftrag vom PC → sofort in der App; die App startet am Raum des Auftrags
        var raum = szene.Raeume.Last();
        var (code, _) = await s.LokalPost("/api/vr/auftrag", new { modus = "begehen", raum = raum.Id, geschoss = eg.Id });
        Assert.Equal(HttpStatusCode.OK, code);
        using (var frist = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var a = VrStartLogik.AuftragPruefen(await auftraege.Reader.ReadAsync(frist.Token))!;
            Assert.Equal(raum.Id, a.Raum);
            Assert.True(VrStartLogik.AuftragNeu(a, 0));
            var neu = await AppStart.Waehlen(a, null, szene, stand.Liste);
            Assert.Equal(Startquellen.Auftrag, neu.Punkt.Quelle);
            Assert.Equal(raum.Id, neu.Punkt.Standort!.Raum);
        }

        // Stellschraube vom PC
        (code, _) = await s.LokalPost("/api/xr/stellschraube", new { schluessel = "pinch.schwelle", wert = 0.02 });
        Assert.Equal(HttpStatusCode.OK, code);
        using (var frist = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            var (k, wert) = await schrauben.Reader.ReadAsync(frist.Token);
            Assert.Equal("pinch.schwelle", k);
            Assert.Equal(0.02, wert.GetDouble());
        }

        // der Server trennt (Zugang aus, 1001): die App verbindet neu
        s.Dienste.GetRequiredService<XrKanal>().AlleTrennen();
        await Warte(() => Task.FromResult(Volatile.Read(ref willkommenAnzahl) >= 2), "kein neues willkommen nach der Trennung");
        Assert.Equal(KanalZustand.Verbunden, kanal.Zustand);
        Assert.Equal(1001, kanal.LetzterSchluss);

        // Widerruf am PC: 4001, die App verbindet NICHT neu; die Probe meldet danach «neu koppeln»
        (code, _) = await s.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = paket.Kopplung });
        Assert.Equal(HttpStatusCode.OK, code);
        await Warte(() => Task.FromResult(kanal.Zustand == KanalZustand.Widerrufen), $"nicht widerrufen: {kanal.Zustand}");
        Assert.Equal(4001, kanal.LetzterSchluss);
        await Task.Delay(600);
        Assert.Equal(2, Volatile.Read(ref willkommenAnzahl));
        Assert.Equal(0, (await s.Zugang())["teilnehmer"]!.GetValue<int>());
        var nachher = await PcSuche.Finden(paket, null, new SucheOptionen { Suchen = false });
        Assert.Equal(PcFundZustand.SchluesselUngueltig, nachher.Zustand);
    }

    [Fact]
    public async Task Zweite_Verbindung_derselben_Brille_ersetzt_die_erste_4002()
    {
        await using var s = new XrServer();
        var (port, _, _) = await s.Aktiv();
        var paket = Kopplungspaket.Lesen((await s.Paket("QUEST-2X")).ToJsonString());
        var adresse = new PcAdresse("127.0.0.1", port, paket.Pc, "paket");
        var optionen = new KanalOptionen { Name = "Quest", Geraet = "Meta Quest 3", Wartezeit = _ => TimeSpan.FromMilliseconds(100) };
        await using var erste = new KanalVerbindung(paket, _ => Task.FromResult<Uri?>(adresse.Kanal), optionen);
        erste.Starten();
        await Warte(() => Task.FromResult(erste.Zustand == KanalZustand.Verbunden), "erste verbindet nicht");
        await using var zweite = new KanalVerbindung(paket, _ => Task.FromResult<Uri?>(adresse.Kanal), optionen);
        zweite.Starten();
        await Warte(() => Task.FromResult(erste.Zustand == KanalZustand.Ersetzt), $"erste nicht ersetzt: {erste.Zustand}");
        Assert.Equal(4002, erste.LetzterSchluss);
        await Warte(() => Task.FromResult(zweite.Zustand == KanalZustand.Verbunden), "zweite verbindet nicht");
        await Task.Delay(500);
        Assert.Equal(KanalZustand.Ersetzt, erste.Zustand);   // kein Hin und Her
        Assert.Equal(1, erste.Verbindungen);
        Assert.Single((await Teilnehmer(s))["teilnehmer"]!.AsArray());
    }

    [Fact]
    public async Task Ohne_gueltigen_Schluessel_meldet_der_Kanal_nicht_gekoppelt()
    {
        await using var s = new XrServer();
        var (port, _, _) = await s.Aktiv();
        var roh = await s.Paket("QUEST-401");
        var echt = (string)roh["schluessel"]!;
        roh["schluessel"] = (echt[0] == 'A' ? "B" : "A") + echt[1..];   // gleiche Form, anderer Schlüssel
        var paket = Kopplungspaket.Lesen(roh.ToJsonString());
        await using var k = new KanalVerbindung(paket, _ => Task.FromResult<Uri?>(new PcAdresse("127.0.0.1", port, paket.Pc, "paket").Kanal),
            new KanalOptionen { Wartezeit = _ => TimeSpan.FromMilliseconds(100) });
        k.Starten();
        await Warte(() => Task.FromResult(k.Zustand == KanalZustand.NichtGekoppelt), $"Zustand {k.Zustand}");
        Assert.Equal(0, k.Verbindungen);
    }
}
