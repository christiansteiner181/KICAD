using System.Net;
using System.Text.Json.Nodes;

namespace Hlks.Labor.Tests;

/// <summary>
/// Das Labor als eigener Prozess, wie das Startskript es startet («dotnet Hlks.Labor.dll --port …»), im Testlauf (HLKS_TEST=1) auf einem freien Port.
/// Bewusst ohne GET /api/xr/zugang: das lädt das Zertifikat, und ein beendeter Prozess gibt dessen Schlüsseldatei nicht mehr frei (P3p, B4).
/// </summary>
public class LaborStartTests
{
    [Fact]
    public async Task Start_auf_freiem_Port_mit_HLKS_TEST_Status_Seite_und_Ablage_unter_TEMP()
    {
        var port = LaborProzess.FreierPort();
        await using var l = LaborProzess.Starten(port);
        var s = await l.StatusAbwarten(TimeSpan.FromSeconds(60));
        Assert.Equal("xr-labor", (string)s["app"]!);
        Assert.False((bool)s["beschaeftigt"]!);
        Assert.Equal("0.1.0", (string)s["fassung"]!);
        Assert.Contains($"XR-Labor läuft auf http://localhost:{port}", l.Ausgabe);

        var seite = await l.Klient.GetStringAsync("/");
        Assert.Contains("<title>XR-Labor</title>", seite);
        Assert.Equal(HttpStatusCode.OK, (await l.Klient.GetAsync("/css/quest.css")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await l.Klient.GetAsync("/js/oberflaeche/quest-app-dialog.js")).StatusCode);

        // im Testlauf nie ein echtes adb, die Ablage der Labor-App unter %TEMP%
        var app = (JsonObject)JsonNode.Parse(await l.Klient.GetStringAsync("/api/quest/app"))!;
        Assert.Equal("ch.hkplanung.xrlabor", (string)app["paket"]!);
        Assert.False((bool)app["adbGefunden"]!);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), Path.GetFullPath((string)app["ablage"]!["ordner"]!));
        Assert.Empty(JsonNode.Parse(await l.Klient.GetStringAsync("/api/xr/teilnehmer"))!["teilnehmer"]!.AsArray());
    }
}
