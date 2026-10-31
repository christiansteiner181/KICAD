using System.Text.Json.Nodes;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Modul 6 Teil A (entwurf/modul-6.md): Lüftungskette am Beispielprojekt «MFH Musterweg» mit FIKTIVEN Luftmengen (Testwerte, keine
/// Normwerte). Handrechnung: Querschnitt = Volumenstrom ÷ 3600 ÷ v, v nach Katalog lueftung.json (MuKEn 2014 Art. 1.19 Abs. 3), runder Kanal
/// d = √(4A/π). Räume: Wohnen .01 (3500/3000) ZUL 120, Zimmer .04 (3500/8000) ZUL 60, Bad .02 (8500/8000) ABL 80, Dusche/WC .03 (11000/8000)
/// ABL 40, je EG und 1.OG; Korridor .05 und das UG ohne Luftmenge. Technik U.01 (2500/2500) ist Kandidat für die Zentrale.</summary>
public class LueftungKetteTests
{
    static double Z(JsonNode? n) => Json.Zahl(n) ?? throw new Xunit.Sdk.XunitException($"Keine Zahl: {n?.ToJsonString() ?? "null"}");
    static double D(double m3h, double ms) => Math.Sqrt(4 * (m3h / 3600 / ms) / Math.PI) * 1000;

    static Projekt Beispiel(bool brandabschnitte = false)
    {
        var p = Beispielprojekt.Erzeugen(Kat, null);
        foreach (var k in new[] { "E", "1" })
        {
            void Setze(string raum, JsonObject eig)
            {
                eig["nutzungseinheit"] = $"W-{k}";
                if (brandabschnitte) eig["brandabschnitt"] = $"BA-{k}";
                Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = raum, ["eigenschaften"] = eig }));
            }
            Setze($"{k}.01", new JsonObject { ["zuluft"] = 120 });
            Setze($"{k}.04", new JsonObject { ["zuluft"] = 60 });
            Setze($"{k}.02", new JsonObject { ["abluft"] = 80 });
            Setze($"{k}.03", new JsonObject { ["abluft"] = 40 });
        }
        if (brandabschnitte)
            Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "U.01", ["eigenschaften"] = new JsonObject { ["brandabschnitt"] = "BA-U" } }));
        return p;
    }

    static JsonObject Rechne(Projekt p, JsonObject? par = null) => (JsonObject)Tuer.Abfrage(p, "lueftung_varianten", par);
    static JsonObject Variante(JsonObject r, string schluessel) => (JsonObject)r["varianten"]!.AsArray().Single(v => v!["variante"]!.GetValue<string>() == schluessel)!;
    static JsonObject Kanal(JsonObject v, string abschnitt) => (JsonObject)v["kanaele"]!.AsArray().Single(k => k!["abschnitt"]!.GetValue<string>() == abschnitt)!;

    [Fact]
    public void Zentral_GeraetInDerTechnik_SteigzoneUndQuerschnitteWieHandrechnung()
    {
        var p = Beispiel();
        var anzahl = p.Modell.Elemente.Count;
        var r = Rechne(p);
        Assert.Equal(anzahl, p.Modell.Elemente.Count);   // rechnet nur
        Assert.Equal(8, Z(r["eingaben"]!["mitLuftmenge"]));
        Assert.Equal(5, Z(r["eingaben"]!["ohneLuftmenge"]));   // U.01–U.03, E.05, 1.05

        var v = Variante(r, "zentral");
        var g = (JsonObject)Assert.Single(v["geraete"]!.AsArray())!;
        Assert.Equal("U.01", g["raum"]!.GetValue<string>());
        Assert.False(g["lageAngenommen"]!.GetValue<bool>());
        Assert.Equal(360, Z(g["zuluftM3h"]));
        Assert.Equal(240, Z(g["abluftM3h"]));

        // Steigzone angenommen in der Mitte der 8 Räume (6625/6750); Anbindung von U.01 rechtwinklig 4125 + 4250 mm
        Assert.Equal(8.4, Z(Kanal(v, "LG-1 Anbindung ZUL")["laengeM"]));
        var steigZul = Kanal(v, "LG-1 Steigzone ZUL UG–EG");
        Assert.Equal(360, Z(steigZul["m3h"]));
        Assert.Equal(3, Z(steigZul["ms"]));
        Assert.Equal(Math.Round(D(360, 3)), Z(steigZul["durchmesserMm"]));
        Assert.Equal(180, Z(Kanal(v, "LG-1 Steigzone ZUL EG–1.OG")["m3h"]));
        Assert.Equal(120, Z(Kanal(v, "LG-1 Steigzone ABL EG–1.OG")["m3h"]));

        // Schacht im UG: ZUL 360 und ABL 240 nebeneinander, Zuschlag 200 mm je Richtung (Christian «20cm», gesamt gerechnet)
        var schacht = (JsonObject)v["raumbedarf"]!["schaechte"]!.AsArray().Single(s => s!["geschoss"]!.GetValue<string>() == "UG")!;
        double dz = D(360, 3), da = D(240, 3);
        Assert.Equal(Math.Round((dz + da + 200) * (dz + 200) / 1e6, 3), Z(schacht["flaecheM2"]));
        Assert.True(schacht["lageAngenommen"]!.GetValue<bool>());
        // Hohlraum UG: grösster Kanal (Anbindung ZUL) + 100 mm
        var hohl = v["raumbedarf"]!["hohlraum"]!.AsArray().Single(h => h!["geschoss"]!.GetValue<string>() == "UG")!;
        Assert.Equal(Math.Round(dz + 100), Z(hohl["noetigMm"]));
        // Zentrale: Mindest-Nettofläche des Geräts bei 2 m/s
        Assert.Equal(Math.Round(360 / 3600.0 / 2, 3), Z(v["raumbedarf"]!["zentralen"]![0]!["nettoflaecheMinM2"]));
        Assert.Null(v["kennzahlen"]!["brandschutzklappen"]);
    }

    [Fact]
    public void Varianten_JeGeschossJeEinheitJeZone_UndBrandschutzklappen()
    {
        var r = Rechne(Beispiel());
        var geschoss = Variante(r, "geschoss");
        Assert.Equal(2, Z(geschoss["kennzahlen"]!["geraete"]));
        Assert.All(geschoss["geraete"]!.AsArray(), x => Assert.False(x!["steigzone"]!.GetValue<bool>()));
        Assert.Contains(geschoss["befunde"]!.AsArray(), b => b!.GetValue<string>().Contains("Lage angenommen"));
        var einheit = Variante(r, "einheit");
        Assert.Equal(2, Z(einheit["kennzahlen"]!["geraeteInNutzungseinheiten"]));
        var zone = Variante(r, "zentral_zone");   // ohne Brandabschnitte: nach Nutzung Bad, Schlafen, Wohnen
        Assert.Equal(3, Z(zone["kennzahlen"]!["geraete"]));
        Assert.Contains(zone["befunde"]!.AsArray(), b => b!.GetValue<string>().StartsWith("Brandabschnitte fehlen"));

        var mitBa = Rechne(Beispiel(brandabschnitte: true));
        Assert.Equal(2, Z(Variante(mitBa, "zentral_zone")["kennzahlen"]!["geraete"]));
        // zentral in U.01 (BA-U): ZUL und ABL je in BA-E und BA-1 → 4 Klappen
        Assert.Equal(4, Z(Variante(mitBa, "zentral")["kennzahlen"]!["brandschutzklappen"]));
    }

    [Fact]
    public void Gleichzeitigkeit_NurAufSummen_UndKanalstufenNachKatalog()
    {
        var v = Variante(Rechne(Beispiel(), new JsonObject { ["gleichzeitigkeit"] = 0.8, ["variante"] = "zentral" }), "zentral");
        Assert.Equal(Math.Round(360 * 0.8), Z(v["geraete"]![0]!["zuluftM3h"]));
        Assert.Throws<HlksFehler>(() => Rechne(Beispiel(), new JsonObject { ["gleichzeitigkeit"] = 1.2 }));

        var kat = Kat.Lueftung!;
        Assert.Equal(3, kat.KanalMs(1000));
        Assert.Equal(4, kat.KanalMs(1000.1));
        Assert.Equal(6, kat.KanalMs(10000));
        Assert.Equal(7, kat.KanalMs(25000));
        Assert.Equal(2, kat.ApparateMs);
    }
}
