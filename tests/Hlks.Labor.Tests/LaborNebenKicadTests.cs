using System.Net;
using Hlks.Web;
using Hlks.Web.Tests;

namespace Hlks.Labor.Tests;

/// <summary>
/// KICAD und Labor gleichzeitig auf einem PC (entwurf/labor-app.md «Ports und Prozesse»): beide Netzzugänge offen, verschiedene Ports, dasselbe Zertifikat, getrennte
/// Kopplungen – eine Brille, die mit KICAD gekoppelt ist, kommt nicht ins Labor und umgekehrt; das Labor ändert KICADs Dateien nie.
/// </summary>
public class LaborNebenKicadTests
{
    [Fact]
    public async Task Beide_Netzzugaenge_offen_dasselbe_Zertifikat_getrennte_Kopplungen()
    {
        await using var kicad = new XrServer(zugang: true);
        var (kPort, kSuch, kFp) = await kicad.Aktiv();
        var kicadZugang = File.ReadAllText(Path.Combine(kicad.XrOrdner, "zugang.json"));
        var kicadPaket = await kicad.Paket("QUEST3-KICAD");
        var kicadKopplungen = File.ReadAllText(Path.Combine(kicad.XrOrdner, "kopplungen.json"));

        // das Labor teilt den Ordner des Zertifikats mit KICAD (wie %LOCALAPPDATA%\HLKS_CAD\xr), Schalter und Kopplungen liegen in …\labor
        await using var labor = new LaborServer(zugang: true, zertifikatOrdner: kicad.XrOrdner);
        var (lPort, lSuch, lFp) = await labor.Aktiv();
        Assert.NotEqual(kPort, lPort);
        Assert.NotEqual(kSuch, lSuch);
        Assert.Equal(kFp, lFp);   // dasselbe Zertifikat je PC
        Assert.True((bool)(await kicad.Zugang())["aktiv"]!);
        var laborPaket = await labor.Paket("QUEST3-LABOR");
        Assert.Equal(lPort, (int)laborPaket["port"]!);
        Assert.Equal(kFp, (string)laborPaket["fingerabdruck"]!);

        // jeder Schlüssel gilt nur an seinem Zugang
        using (var k = XrServer.Netz(kPort, kFp, XrServer.Token(kicadPaket))) Assert.Equal(HttpStatusCode.OK, (await k.GetAsync("/api/xr/verbindung")).StatusCode);
        using (var k = XrServer.Netz(lPort, lFp, XrServer.Token(kicadPaket))) Assert.Equal(HttpStatusCode.Unauthorized, (await k.GetAsync("/api/xr/verbindung")).StatusCode);
        using (var l = XrServer.Netz(lPort, lFp, XrServer.Token(laborPaket))) Assert.Equal(HttpStatusCode.OK, (await l.GetAsync("/api/xr/verbindung")).StatusCode);
        using (var l = XrServer.Netz(kPort, kFp, XrServer.Token(laborPaket))) Assert.Equal(HttpStatusCode.Unauthorized, (await l.GetAsync("/api/xr/verbindung")).StatusCode);
        Assert.Equal(["QUEST3-KICAD"], (await kicad.Zugang())["kopplungen"]!.AsArray().Select(x => (string)x!["brille"]!).ToArray());
        Assert.Equal(["QUEST3-LABOR"], (await labor.Zugang())["kopplungen"]!.AsArray().Select(x => (string)x!["brille"]!).ToArray());

        // das Labor schaltet seinen Zugang aus und widerruft seine Kopplung: KICADs Dateien bleiben, wie sie waren, KICADs Zugang läuft weiter
        Assert.Equal(HttpStatusCode.OK, (await labor.LokalPost("/api/xr/kopplung/widerrufen", new { kopplung = (string)laborPaket["kopplung"]! })).Code);
        Assert.Equal(HttpStatusCode.OK, (await labor.LokalPost("/api/xr/zugang", new { an = false })).Code);
        Assert.Equal(kicadZugang, File.ReadAllText(Path.Combine(kicad.XrOrdner, "zugang.json")));
        Assert.Equal(kicadKopplungen, File.ReadAllText(Path.Combine(kicad.XrOrdner, "kopplungen.json")));
        Assert.True((bool)(await kicad.Zugang())["aktiv"]!);
        Assert.Equal(["zugang.json", "kopplungen.json"], new[] { "zugang.json", "kopplungen.json" }.Where(n => File.Exists(Path.Combine(labor.LaborOrdner, n))).ToArray());
        using (var k = XrServer.Netz(kPort, kFp, XrServer.Token(kicadPaket))) Assert.Equal(HttpStatusCode.OK, (await k.GetAsync("/api/xr/verbindung")).StatusCode);
    }
}
