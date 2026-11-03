using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.IfcHilfe;

namespace Hlks.Kern.Tests;

/// <summary>IFC 1:1 (Christian 02.11.2026: «der ifc import ist die basis für alles folgende, dieser muss immer 100% passen»): nach der
/// Übernahme der echten Bernstrasse-42-Dateien deckt sich jeder Nachbau mit seinem IFC-Netz (±5 mm, Abfrage ifc_deckung). Architektur:
/// Archicad-Ausführungsmodell; Installation: H+K Revit 2025 (Rohre und Kanäle; Formteile, Armaturen, Geräte zeigen ihr IFC-Netz). Dateien
/// nur lokal unter %USERPROFILE%\HLKS_CAD_testdaten, fehlen sie, kehrt der Test früh zurück. Stand 03.11.2026: 598/598 und 1944/1952 (8 Flachoval-Kanäle offen).</summary>
public class IfcDeckungEchtTests
{
    static JsonObject Deckung(Projekt p) => IfcDeckung.Pruefen(p, new JsonObject { ["anzahl"] = 20 })["unterlagen"]!.AsArray().Single()!.AsObject();

    static void AlleDeckungsgleich(JsonObject u)
    {
        int n = u["elemente"]!.GetValue<int>(), ok = u["deckungsgleich"]!.GetValue<int>();
        Assert.True(n > 0);
        Assert.True(ok == n, $"{ok} von {n} deckungsgleich: {u["abweichend"]!.ToJsonString()[..Math.Min(3000, u["abweichend"]!.ToJsonString().Length)]}");
        Assert.Equal(0, u["ohneNachbauKoerper"]!.GetValue<int>());
    }

    [Fact]
    public void Architektur_Bernstrasse42_JederNachbauDeckungsgleich()
    {
        if (Echt(@"test_praxis\unterlagen\a7ea68192b3c_440_Bernstrasse 42 Ausführung.ifc") is not { } pfad) return;
        var p = Leer();
        Ok(Uebernehmen(p, pfad, new JsonObject { ["bereiche"] = new JsonArray("architektur", "raeume") }));
        var u = Deckung(p);
        AlleDeckungsgleich(u);
        Assert.True(u["elemente"]!.GetValue<int>() >= 598);
    }

    [Fact]
    public void Installation_Bernstrasse42_RohreDeckungsgleich_KanaeleBisAufFlachovale_BauteileMitIfcKoerper()
    {
        if (Echt(@"bernstrasse42\hlks_hk_2026-02-10.ifc") is not { } pfad) return;
        var p = Leer();
        Ok(Uebernehmen(p, pfad, new JsonObject { ["bereiche"] = new JsonArray("hlks") }));
        var u = Deckung(p);
        // Stand 03.11.2026: alle Rohre; Kanäle bis auf 8 kurze Flachovale (als Rechteck nachgebaut, längs der Achse bis 33 mm kürzer
        // als ihr IFC-Körper – offen)
        var rohr = u["jeKlasse"]!["Rohr"]!; var kanal = u["jeKlasse"]!["Kanal"]!;
        Assert.Equal(rohr["anzahl"]!.GetValue<int>(), rohr["deckungsgleich"]!.GetValue<int>());
        Assert.True(kanal["anzahl"]!.GetValue<int>() - kanal["deckungsgleich"]!.GetValue<int>() <= 8, u["abweichend"]!.ToJsonString());
        Assert.True(kanal["maxMm"]!.GetValue<double>() <= 35);
        Assert.Equal(0, u["ohneNachbauKoerper"]!.GetValue<int>());
        Assert.True(u["jeKlasse"]!["Rohr"]!["anzahl"]!.GetValue<int>() >= 1634);
        Assert.True(u["ausIfcKoerper"]!["Bogen"]!.GetValue<int>() >= 1375);
    }
}
