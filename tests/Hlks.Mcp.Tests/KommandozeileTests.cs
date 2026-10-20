using System.Text;
using System.Text.Json.Nodes;
using static Hlks.Mcp.Tests.Hilfe;

namespace Hlks.Mcp.Tests;

/// <summary>Kommandozeile über der gebauten Hlks.Mcp.dll (echte Prozesse, stdout als UTF-8).</summary>
public class KommandozeileTests
{
    [Fact]
    public void KiAuftrag_EnthaeltAlleZehnFragenOhneProjekt()
    {
        var (code, aus, _) = Lauf("--ki-auftrag");
        Assert.Equal(0, code);
        for (int i = 1; i <= 10; i++) Assert.Contains($"f{i}:", aus);
    }

    [Fact]
    public void Beispiel_Abfrage_Erwartung_Bewertung_DurchgaengigMitUmlautPfad()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        try
        {
            // anlegen
            var (c1, a1, _) = Lauf("--beispiel", ordner);
            Assert.Equal(0, c1);
            Assert.Contains("MFH Musterweg", a1);
            Assert.True(File.Exists(Path.Combine(ordner, "projekt.json")));

            // zweites Mal: Fehler, Code 1
            var (c2, a2, _) = Lauf("--beispiel", ordner);
            Assert.Equal(1, c2);
            Assert.Contains("schon ein Projekt", a2);

            // Abfrage
            var (c3, a3, _) = Lauf("--projekt", ordner, "--abfrage", "strangliste");
            Assert.Equal(0, c3);
            Assert.Equal(9, JsonNode.Parse(a3)!["zeilen"]!.AsArray().Count);   // 7 Sanitär-Stränge + 2 Kälte-Stränge

            // Abfrage mit Parameter und Textansicht, Fehlerfall
            var (c3b, a3b, _) = Lauf("--projekt", ordner, "--abfrage", "strang_text", """{"strang":"SW-F2"}""");
            Assert.Equal(0, c3b);
            Assert.Contains("SW-F2", a3b);
            var (c3c, a3c, _) = Lauf("--projekt", ordner, "--abfrage", "element_lesen", """{"element":"GIBT-ES-NICHT"}""");
            Assert.Equal(1, c3c);
            Assert.Contains("vorschlag", a3c);
            var (c3d, a3d, _) = Lauf("--projekt", ordner, "--abfrage", "gibt_es_nicht");
            Assert.Equal(1, c3d);
            Assert.Contains("Abfragen:", a3d);

            // Erwartung
            var (c4, a4, _) = Lauf("--projekt", ordner, "--ki-erwartung");
            Assert.Equal(0, c4);
            var erwartung = JsonNode.Parse(a4)!.AsObject();
            for (int i = 1; i <= 10; i++) Assert.True(erwartung.ContainsKey($"f{i}"), $"f{i} fehlt");

            // Bewertung: richtige Antwort -> 0, richtig = 10
            var antwort = Path.Combine(wurzel, "antwort.json");
            File.WriteAllText(antwort, erwartung.ToJsonString(), new UTF8Encoding(false));
            var (c5, a5, _) = Lauf("--projekt", ordner, "--ki-bewerten", antwort);
            Assert.Equal(0, c5);
            var b5 = JsonNode.Parse(a5)!;
            Assert.Equal(10, b5["richtig"]!.GetValue<int>());
            Assert.True(b5["bestanden"]!.GetValue<bool>());

            // falsche Antwort -> 2
            var falsch = Path.Combine(wurzel, "falsch.json");
            File.WriteAllText(falsch, """{ "f1": ["gibt-es-nicht"], "f5": 1.0 }""", new UTF8Encoding(false));
            var (c6, a6, _) = Lauf("--projekt", ordner, "--ki-bewerten", falsch);
            Assert.Equal(2, c6);
            Assert.False(JsonNode.Parse(a6)!["bestanden"]!.GetValue<bool>());

            // ungültiges JSON -> als leeres Objekt bewertet -> 2
            var kaputt = Path.Combine(wurzel, "kaputt.json");
            File.WriteAllText(kaputt, "das ist kein json", new UTF8Encoding(false));
            Assert.Equal(2, Lauf("--projekt", ordner, "--ki-bewerten", kaputt).Code);
        }
        finally { Aufraeumen(wurzel); }
    }

    [Fact]
    public void Befehl_TrockenlaufUndEchtUndAbgelehnt()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        try
        {
            Assert.Equal(0, Lauf("--beispiel", ordner).Code);
            string Stand() => JsonNode.Parse(Lauf("--projekt", ordner, "--abfrage", "projekt_ueberblick").Aus)!["modellStand"]!["pruefsumme"]!.GetValue<string>();
            var start = Stand();

            var (c1, a1, _) = Lauf("--projekt", ordner, "--befehl", "element_aendern", """{"element":"E.02","name":"Via CLI"}""", "--trockenlauf");
            Assert.Equal(0, c1);
            Assert.True(JsonNode.Parse(a1)!["trockenlauf"]!.GetValue<bool>());
            Assert.Equal(start, Stand());

            var (c2, a2, _) = Lauf("--projekt", ordner, "--befehl", "element_aendern", """{"element":"E.02","name":"Via CLI"}""", "--akteur", "Skript");
            Assert.Equal(0, c2);
            Assert.NotNull(JsonNode.Parse(a2)!["paketId"]);
            Assert.NotEqual(start, Stand());
            Assert.Contains("Via CLI", Lauf("--projekt", ordner, "--abfrage", "element_lesen", """{"element":"E.02"}""").Aus);

            // abgelehnt -> Code 1
            var (c3, a3, _) = Lauf("--projekt", ordner, "--befehl", "element_loeschen", """{"element":"SW-F2"}""");
            Assert.Equal(1, c3);
            Assert.False(JsonNode.Parse(a3)!["angenommen"]!.GetValue<bool>());

            // unbekannter Befehl, ungültiges JSON
            Assert.Equal(1, Lauf("--projekt", ordner, "--befehl", "gibt_es_nicht", "{}").Code);
            var (c4, a4, _) = Lauf("--projekt", ordner, "--befehl", "element_loeschen", "{kaputt");
            Assert.Equal(1, c4);
            Assert.Contains("Fehler", a4);
        }
        finally { Aufraeumen(wurzel); }
    }

    [Fact]
    public void ProjektFehlt_OderOrdnerLeer_GibtVerstaendlicheMeldungCode1()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        try
        {
            var (c, a, _) = Lauf("--projekt", ordner, "--abfrage", "strangliste");
            Assert.Equal(1, c);
            Assert.Contains("kein Projekt", a);
            Assert.DoesNotContain("   at ", a);                       // kein Stacktrace
            var (c2, a2, _) = Lauf("--abfrage", "strangliste");
            Assert.Equal(1, c2);
            Assert.Contains("--projekt", a2);
        }
        finally { Aufraeumen(wurzel); }
    }

    [Fact]
    public void FlacheNotform_OhneAnfuehrungszeichen_FuerWindowsPowerShell()
    {
        var (wurzel, ordner) = TempProjektOrdner();
        try
        {
            Assert.Equal(0, Lauf("--beispiel", ordner).Code);
            var (c, a, _) = Lauf("--projekt", ordner, "--abfrage", "strang_text", "{strang:SW-F2}");
            Assert.Equal(0, c);
            Assert.Contains("SW-F2", a);
            var datei = Path.Combine(wurzel, "p.json");
            File.WriteAllText(datei, """{"strang":"KW-S1"}""", new UTF8Encoding(false));
            var (c2, a2, _) = Lauf("--projekt", ordner, "--abfrage", "strang_text", "@" + datei);
            Assert.Equal(0, c2);
            Assert.Contains("KW-S1", a2);
        }
        finally { Aufraeumen(wurzel); }
    }
}
