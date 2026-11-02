using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;
using static Hlks.Kern.Tests.GrundlagenHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Herstellerbibliothek H2/H3 (entwurf/herstellerbibliothek.md): VDI-3805-Leser (nur Struktur), Zweitbeleg in der Wertprüfung,
/// Abgleich mit der Typenliste. Die Testdatei ist FIKTIV («Musterpumpe AG»), im Aufbau wie die KSB-Pakete (Zeilen, «;», Satzart vorn,
/// Windows-1252); Feldinhalte sind erfunden und nicht gedeutet.</summary>
public class VdiLeserTests
{
    static readonly string Muster =
        "010;4;202104;Musterpumpe AG;20261102;\r\n" +
        "110;10;;Musterreihe;Umwälzpumpe;\r\n" +
        "700;1;;Musterreihe 25-40, G1 1/2, 230V;Flansch;230;IP44;\r\n" +
        "710.03;1;1;150;102;130;2.7;\r\n" +
        "700;2;;Musterreihe 25-60, G1 1/2, 230V;Flansch;230;IP44;\r\n" +
        "700;3;;Musterreihe 32-80, G2, 230V;Flansch;230;IP44;\r\n" +
        "Kommentar ohne Satzart\r\n";

    static string Datei(Ordner o, string inhalt)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var pfad = o.Datei("muster.vdi");
        File.WriteAllBytes(pfad, Encoding.GetEncoding(1252).GetBytes(inhalt));
        return pfad;
    }

    [Fact]
    public void Leser_ZerlegtSaetzeUndFelder_OhneDeutung()
    {
        using var o = new Ordner();
        var b = new VdiBefundLeser().Lesen(Datei(o, Muster), "muster.vdi", "abcdefabcdef00", "vdi3805");
        Assert.Equal(6, b.Befunde.Count);   // Kommentarzeile ohne Satzart zählt nicht
        Assert.All(b.Befunde, x => Assert.Equal("satz", x.Art));
        var zweite = b.Befunde[1];
        Assert.Equal("110", zweite.Wert!["satzart"]!.GetValue<string>());
        Assert.Equal(["10", "", "Musterreihe", "Umwälzpumpe"], VdiFelder(zweite));   // Windows-1252 «ä», abschliessendes «;» ohne leeres Feld
        Assert.Equal(2, zweite.Zusatz!["zeile"]!.GetValue<int>());
        Assert.Equal("710.03", b.Befunde[3].Wert!["satzart"]!.GetValue<string>());
        Assert.Equal("abcdefabcdef#4", b.Befunde[3].Id);
    }

    static string[] VdiFelder(UnterlagenBefund b) => (b.Wert!["felder"] as JsonArray)!.Select(x => x!.GetValue<string>()).ToArray();

    [Fact]
    public void Leser_HaeufigeSatzartNurGezaehlt()
    {
        using var o = new Ordner();
        var sb = new StringBuilder("010;4;;Musterpumpe AG;\r\n");
        for (int i = 0; i <= VdiBefundLeser.MaxJeSatzart; i++) sb.Append("970.02;1;0;0;0;\r\n");
        sb.Append("700;1;;Musterreihe 25-40;\r\n");
        var b = new VdiBefundLeser().Lesen(Datei(o, sb.ToString()), "muster.vdi", "abcdefabcdef00", "vdi3805");
        Assert.Equal(2, b.Befunde.Count);
        Assert.Contains(b.Hinweise, h => h.StartsWith("Satzart 970.02:"));
    }

    [Fact]
    public void Zweitbeleg_UndAbgleichMitTypenliste()
    {
        using var o = new Ordner();
        var p = FamiliePruefungHilfe.ProjektB4();
        var vdi = FamiliePruefungHilfe.Ablegen(p, Datei(o, Muster));
        var satz2 = vdi.Befunde.Single(x => x.Wert!["satzart"]!.GetValue<string>() == "700" && VdiFelder(x)[0] == "2").Id;

        JsonObject Familie(double laenge) => FamilieTestHilfe.JsonAus($$"""
            { "format": 1, "name": "Musterreihe (fiktiv)", "kategorie": "Pumpe",
              "parameter": [ { "name": "l", "art": "typ", "datentyp": "laenge", "gruppe": "abmessung" },
                             { "name": "schutzart", "art": "typ", "datentyp": "text", "gruppe": "kennwert" } ],
              "typen": [ { "name": "Musterreihe 25-40", "werte": { "l": 180, "schutzart": "IP44" } },
                         { "name": "Musterreihe 25-60", "werte": { "l": {{laenge}}, "schutzart": "IP44" }, "quellen": { "schutzart": [ "{{satz2}}" ] } },
                         { "name": "Musterreihe 40-100", "werte": { "l": 220, "schutzart": "IP44" } } ],
              "koerper": [], "anschluesse": [] }
            """);
        // Text aus einem Feld des Satzes passt (Zweitbeleg)
        Ok(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = Familie(180) }));

        var a = (JsonObject)Tuer.Abfrage(p, "vdi_abgleich", new JsonObject { ["familie"] = "Musterreihe (fiktiv)", ["unterlage"] = "muster.vdi", ["satzart"] = "700" });
        Assert.Equal(["Musterreihe 40-100"], (a["typenOhneSatz"] as JsonArray)!.Select(x => x!.GetValue<string>()));
        var ohne = Assert.Single((a["saetzeOhneTyp"] as JsonArray)!);
        Assert.Contains("32-80", ohne!["text"]!.GetValue<string>());
        // «25-40» passt nicht auf den Satz mit «25-60» (ganze Wörter)
        Assert.Equal(2, (a["zuordnung"] as JsonArray)!.Count);

        var uebersicht = (JsonObject)Tuer.Abfrage(p, "vdi_abgleich", new JsonObject { ["familie"] = "Musterreihe (fiktiv)", ["unterlage"] = "muster.vdi" });
        Assert.Contains((uebersicht["satzarten"] as JsonArray)!, s => s!["satzart"]!.GetValue<string>() == "700" && s["anzahl"]!.GetValue<int>() == 3);
    }

    [Fact]
    public void Zweitbeleg_FalscherWertWirdAbgelehnt()
    {
        using var o = new Ordner();
        var p = FamiliePruefungHilfe.ProjektB4();
        var vdi = FamiliePruefungHilfe.Ablegen(p, Datei(o, Muster));
        var satz1 = vdi.Befunde.Single(x => x.Wert!["satzart"]!.GetValue<string>() == "700" && VdiFelder(x)[0] == "1").Id;
        var d = FamilieTestHilfe.JsonAus($$"""
            { "format": 1, "name": "Musterreihe falsch (fiktiv)", "kategorie": "Pumpe",
              "parameter": [ { "name": "u", "art": "typ", "datentyp": "zahl", "einheit": "V", "gruppe": "kennwert" } ],
              "typen": [ { "name": "Musterreihe 25-40", "werte": { "u": 400 }, "quellen": { "u": [ "{{satz1}}" ] } } ],
              "koerper": [], "anschluesse": [] }
            """);
        var e = Nein(p.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = d }));
        Assert.Contains("weicht", e.Fehler);
    }

    [Fact]
    public void Echt_KsbPumpen_WennVorhanden()
    {
        var ordner = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "HLKS_CAD_testdaten", "vdi3805", "ksb");
        var datei = Directory.Exists(ordner) ? Directory.EnumerateFiles(ordner, "PART04*.vdi", SearchOption.AllDirectories).FirstOrDefault() : null;
        if (datei is null) return;   // echte Herstellerdaten nur lokal (nie im Repository)
        var b = new VdiBefundLeser().Lesen(datei, Path.GetFileName(datei), "0123456789ab00", "vdi3805");
        Assert.Contains(b.Befunde, x => x.Wert!["satzart"]!.GetValue<string>() == "700");
        Assert.Contains(b.Hinweise, h => h.Contains("Windows-1252"));
    }
}
