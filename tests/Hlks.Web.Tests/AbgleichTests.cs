using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Kern;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Hlks.Web.Tests;

/// <summary>
/// Live-Abgleich zwischen Arbeitsplätzen (Abnahme Modul 0b: «Eine Änderung erscheint sofort in allen offenen Ansichten
/// und nach wenigen Sekunden auf einem zweiten PC»). Ein Arbeitsplatz = ein Web-Server (TestServer) mit eigenem Gerät
/// und Ereignisstrom (/api/ereignisse). Zweiter Arbeitsplatz = ein zweites Projekt des Kerns auf demselben Ordner oder
/// die Kommandozeile Hlks.Mcp als eigener Prozess (KI-/zweiter-PC-Fall). Temp-Ordner mit «ä» und Leerzeichen.
/// Zeiten sind grosszügig (paralleler Bau), gemessen und gegen die Abnahme-Grenze 3 s geprüft wird nur die Zeit vom
/// Paket des anderen Arbeitsplatzes bis zum Ereignis.
/// </summary>
public class AbgleichTests(ITestOutputHelper ausgabe)
{
    static readonly Katalog Kat = Katalog.Laden(Katalog.StandardOrdner());
    static readonly TimeSpan Abnahme = TimeSpan.FromSeconds(3);
    static readonly TimeSpan Lang = TimeSpan.FromSeconds(20);
    const string Dotnet = @"C:\Program Files\dotnet\dotnet.exe";

    // ================================================================== eigene und fremde Änderungen

    [Fact]
    public async Task EigeneAenderung_KommtSofortAlsLokalesEreignis_UndNurEinmal()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        Assert.Empty(Sperren(ordner));   // keine Reservierungen des Beispielgenerators stehen lassen
        var strom = await a.Verbinden();
        var id = await a.ElementId("WC-E.02");

        var uhr = Stopwatch.StartNew();
        var erg = await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "WC lokal" });
        var paket = T(erg, "paketId")!;
        var (_, e) = await strom.Warten(x => Art(x) == "aenderung" && T(x, "paket") == paket, TimeSpan.FromSeconds(5), "eigene Änderung");
        var dauer = uhr.Elapsed;
        ausgabe.WriteLine($"Eigene Änderung bis Ereignis (inkl. Befehl): {dauer.TotalMilliseconds:0} ms");
        Assert.Equal("lokal", T(e, "quelle"));
        Assert.Contains(id, Ids(e));
        Assert.NotNull(e["stand"]);
        Assert.True(dauer < TimeSpan.FromSeconds(1), $"eigene Änderung erst nach {dauer.TotalMilliseconds:0} ms gemeldet");

        // nicht doppelt: der Abgleich (Wächter + zwei Takte) darf das eigene Paket nicht noch einmal als fremd melden
        await Task.Delay(a.Einstellungen.Takt * 2 + TimeSpan.FromMilliseconds(500));
        Assert.Single(strom.Alle(x => Art(x) == "aenderung" && T(x, "paket") == paket));
    }

    [Fact]
    public async Task FremdeAenderung_ZweiterArbeitsplatzKern_KommtInnertDreiSekunden()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            var id = zweiter.Modell.Finde("WC-E.02")!.Id;
            var zeiten = new List<TimeSpan>();
            for (int i = 1; i <= 3; i++)
            {
                var name = $"von pc2 #{i}";
                var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = name });
                Assert.True(erg.Angenommen, erg.Fehler);
                var (zeit, e) = await strom.Warten(x => Art(x) == "aenderung" && T(x, "paket") == erg.PaketId, Lang, "fremde Änderung");
                Assert.Equal("fremd", T(e, "quelle"));
                Assert.Equal("MB", T(e, "akteur"));
                Assert.Contains(id, Ids(e));
                zeiten.Add(zeit - PaketZeit(erg.PaketId!));
                Assert.Equal(name, T(await a.ElementAusModell("WC-E.02"), "name"));
            }
            Bericht("Fremde Änderung (Kern, Dateiwächter)", zeiten);
            Assert.All(zeiten, z => Assert.True(z < Abnahme, $"fremde Änderung erst nach {z.TotalMilliseconds:0} ms"));
            var status = await a.Status();
            Assert.NotNull(T(status["abgleich"], "waechter"));
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task FremdeAenderung_OhneDateiwaechter_KommtUeberDenTakt()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1", e => e with { DateiWaechter = false });
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            var zeiten = new List<TimeSpan>();
            for (int i = 1; i <= 3; i++)
            {
                var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1.02", ["name"] = $"Takt {i}" });
                Assert.True(erg.Angenommen, erg.Fehler);
                var (zeit, e) = await strom.Warten(x => Art(x) == "aenderung" && T(x, "paket") == erg.PaketId, Lang, "fremde Änderung über den Takt");
                Assert.Equal("fremd", T(e, "quelle"));
                zeiten.Add(zeit - PaketZeit(erg.PaketId!));
            }
            Bericht("Fremde Änderung (Kern, nur Takt 2 s)", zeiten);
            Assert.All(zeiten, z => Assert.True(z < Abnahme, $"fremde Änderung erst nach {z.TotalMilliseconds:0} ms"));
            Assert.Null(T((await a.Status())["abgleich"], "waechter"));
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task FremdeAenderung_KiUeberKommandozeile_EigenerProzess()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var id = await a.ElementId("WC-E.03");

        var (code, aus, fehler) = await Cli("--projekt", ordner, "--geraet", "pc2", "--akteur", "KI-Test", "--art", "ki",
            "--befehl", "element_aendern", """{"element":"WC-E.03","name":"von der KI"}""");
        Assert.True(code == 0, $"Kommandozeile Code {code}: {aus} {fehler}");
        var paket = T(JsonNode.Parse(aus), "paketId")!;
        var (zeit, e) = await strom.Warten(x => Art(x) == "aenderung" && T(x, "paket") == paket, Lang, "Änderung der KI");
        var dauer = zeit - PaketZeit(paket);
        Bericht("Fremde Änderung (Kommandozeile, eigener Prozess, KI)", [dauer]);
        Assert.Equal("fremd", T(e, "quelle"));
        Assert.Equal("ki", T(e, "akteurArt"));
        Assert.Contains(id, Ids(e));
        Assert.True(dauer < Abnahme, $"Änderung der KI erst nach {dauer.TotalMilliseconds:0} ms");
        Assert.Equal("von der KI", T(await a.ElementAusModell("WC-E.03"), "name"));
    }

    [Fact]
    public async Task VieleEigeneAenderungenHintereinander_KeinEreignisGehtVerloren()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var pakete = new List<string>();
        for (int i = 0; i < 30; i++)
            pakete.Add(T(await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = $"n{i}" }), "paketId")!);
        await strom.Warten(x => T(x, "paket") == pakete[^1], Lang, "letzte Änderung");
        var erhalten = strom.Alle(x => Art(x) == "aenderung").Select(x => T(x, "paket") ?? "").ToList();
        Assert.Equal(pakete, erhalten.Where(pakete.Contains).ToList());
    }

    // ================================================================== Ablage weg, hängend, kaputt

    [Fact]
    public async Task AblageWegUndZurueck_MeldetFehlerNurEinmal_DanachWiederVerbunden()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var aenderungen = Path.Combine(ordner, "aenderungen");
        var weg = aenderungen + "_weg";

        // «aenderungen» wegnehmen (den ganzen Ordner sperrt der Dateiwächter, wie auf dem NAS jede offene Datei)
        await Umbenennen(aenderungen, weg);
        var (_, f) = await strom.Warten(x => Art(x) == "abgleich" && T(x, "fehler") is not null, Lang, "Abgleich mit Fehler");
        Assert.Equal("getrennt", T(f, "ablage"));
        Assert.Contains("nicht erreichbar", T(f, "fehler"));
        var status = await a.Status();
        Assert.Equal("getrennt", T(status, "ablage"));
        Assert.NotNull(T(status, "abgleichFehler"));
        // nur beim Zustandswechsel melden, nicht in jedem Takt
        await Task.Delay(a.Einstellungen.Takt * 2 + TimeSpan.FromMilliseconds(500));
        Assert.Single(strom.Alle(x => Art(x) == "abgleich"));
        // Oberfläche bleibt bedienbar
        Assert.NotNull(await a.ElementAusModell("WC-E.02"));

        await Umbenennen(weg, aenderungen);
        var (_, w) = await strom.Warten(x => Art(x) == "abgleich" && x["wiederVerbunden"]?.GetValue<bool>() == true, Lang, "wieder verbunden");
        Assert.Null(T(w, "fehler"));
        Assert.Equal("verbunden", T(w, "ablage"));
        status = await a.Status();
        Assert.Null(T(status, "abgleichFehler"));
        Assert.Equal("verbunden", T(status, "ablage"));
        await WartenBis(async () => T((await a.Status())["abgleich"], "waechter") is not null, Lang, "Dateiwächter wieder aufgesetzt");

        // und weiter geht's: fremde Änderung kommt wieder an
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "nach Ausfall" });
            Assert.True(erg.Angenommen, erg.Fehler);
            var (zeit, _) = await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "fremde Änderung nach Ausfall");
            Bericht("Fremde Änderung nach Wiederverbinden", [zeit - PaketZeit(erg.PaketId!)]);
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task NetzWeg_IOException_MeldetGetrennt_UndNachRueckkehrWiederVerbunden()
    {
        using var temp = new TempOrdner();
        var schalter = new Schalter();
        await using var a = new Arbeitsplatz("pc1", e => e with { SpeicherFabrik = o => new SchaltbarerSpeicher(new OrdnerSpeicher(o), schalter) });
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        schalter.Weg = true;
        var (_, f) = await strom.Warten(x => Art(x) == "abgleich" && T(x, "fehler") is not null, Lang, "Abgleich mit Fehler");
        Assert.Contains("nachgestellt", T(f, "fehler"));
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            // während des Ausfalls schreibt ein anderer Arbeitsplatz weiter (er hat Netz)
            var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "während Ausfall" });
            Assert.True(erg.Angenommen, erg.Fehler);
            await Task.Delay(a.Einstellungen.Takt + TimeSpan.FromMilliseconds(500));
            Assert.Empty(strom.Alle(x => T(x, "paket") == erg.PaketId));
            schalter.Weg = false;
            await strom.Warten(x => Art(x) == "abgleich" && x["wiederVerbunden"]?.GetValue<bool>() == true, Lang, "wieder verbunden");
            await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "nachgeholte fremde Änderung");
            Assert.Equal("während Ausfall", T(await a.ElementAusModell("WC-E.02"), "name"));
            Assert.Single(strom.Alle(x => Art(x) == "abgleich" && T(x, "fehler") is not null));
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task HaengendeAblage_BlockiertOberflaecheNicht_WachhundMeldetUndLoestSich()
    {
        using var temp = new TempOrdner();
        var schalter = new Schalter();
        await using var a = new Arbeitsplatz("pc1", e => e with
        {
            SpeicherFabrik = o => new SchaltbarerSpeicher(new OrdnerSpeicher(o), schalter),
            Wachhund = TimeSpan.FromSeconds(1.5),
        });
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            schalter.Anhalten();
            var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "hinter hängender Ablage" });
            Assert.True(erg.Angenommen, erg.Fehler);
            var (_, f) = await strom.Warten(x => Art(x) == "abgleich" && T(x, "fehler") is not null, Lang, "Wachhund «antwortet nicht»");
            Assert.Contains("antwortet nicht", T(f, "fehler"));

            // Oberfläche bleibt bedienbar, solange der Abgleich an der Ablage hängt
            var uhr = Stopwatch.StartNew();
            Assert.NotNull(await a.ElementAusModell("WC-E.02"));
            var modellZeit = uhr.Elapsed;
            uhr.Restart();
            var status = await a.Status();
            var statusZeit = uhr.Elapsed;
            ausgabe.WriteLine($"Bei hängender Ablage: /api/modell {modellZeit.TotalMilliseconds:0} ms, /api/status {statusZeit.TotalMilliseconds:0} ms");
            Assert.True(modellZeit < TimeSpan.FromSeconds(2), $"/api/modell brauchte {modellZeit.TotalMilliseconds:0} ms");
            Assert.True(statusZeit < TimeSpan.FromSeconds(3), $"/api/status brauchte {statusZeit.TotalMilliseconds:0} ms");
            Assert.Equal("getrennt", T(status, "ablage"));
            Assert.Empty(strom.Alle(x => T(x, "paket") == erg.PaketId));

            schalter.Loslassen();
            await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "fremde Änderung nach dem Hängen");
            await strom.Warten(x => Art(x) == "abgleich" && x["wiederVerbunden"]?.GetValue<bool>() == true, Lang, "wieder verbunden");
            Assert.Single(strom.Alle(x => Art(x) == "abgleich" && T(x, "fehler") is not null));
        }
        finally { schalter.Loslassen(); zweiter.Freigeben(); }
    }

    [Fact]
    public async Task KaputtesPaket_BlockiertNicht_WirdEinmalGemeldet()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        // sortiert vor allen anderen Paketen, Inhalt kein JSON
        const string kaputt = "20000101T000000000Z_kaputt_x_000001";
        var datei = Path.Combine(ordner, "aenderungen", kaputt + ".json");
        await File.WriteAllTextAsync(datei, "{ \"id\": \"abgebrochen");
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "trotz kaputtem Paket" });
            Assert.True(erg.Angenommen, erg.Fehler);
            var (zeit, _) = await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "fremde Änderung trotz kaputtem Paket");
            Bericht("Fremde Änderung mit kaputtem Paket davor", [zeit - PaketZeit(erg.PaketId!)]);
            var (_, f) = await strom.Warten(x => Art(x) == "abgleich" && T(x, "fehler") is not null, Lang, "Meldung kaputtes Paket");
            Assert.Contains(kaputt, T(f, "fehler"));
            Assert.Equal("verbunden", T(f, "ablage"));
            var status = await a.Status();
            Assert.Contains(kaputt, status["abgleich"]!["unlesbar"]!.AsArray().Select(x => x!.GetValue<string>()));

            // in späteren Takten nicht erneut melden
            await Task.Delay(a.Einstellungen.Takt * 2 + TimeSpan.FromMilliseconds(500));
            Assert.Single(strom.Alle(x => Art(x) == "abgleich"));

            // Datei weg → Fehler verschwindet (mit Ereignis)
            File.Delete(datei);
            await strom.Warten(x => Art(x) == "abgleich" && T(x, "fehler") is null, Lang, "Fehler behoben");
            Assert.Null(T(await a.Status(), "abgleichFehler"));
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task VielePakete_InEtappenAbgeglichen_SammelereignisseUndGleicherStand()
    {
        using var temp = new TempOrdner();
        var schalter = new Schalter();
        await using var a = new Arbeitsplatz("pc1", e => e with
        {
            SpeicherFabrik = o => new SchaltbarerSpeicher(new OrdnerSpeicher(o), schalter),
            EtappeMax = 5, EinzelEreignisseMax = 3, Wachhund = TimeSpan.FromSeconds(60),
        });
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            // Abgleich anhalten, bis alle Pakete liegen – dann sieht er sie auf einmal (wie nach einem Netzausfall)
            schalter.Anhalten();
            var pakete = new List<string>();
            string[] elemente = ["WC-E.02", "WC-E.03", "WC-1.02", "WC-1.03"];
            for (int i = 0; i < 23; i++)
            {
                var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = elemente[i % 4], ["name"] = $"Etappe {i}" });
                Assert.True(erg.Angenommen, erg.Fehler);
                pakete.Add(erg.PaketId!);
            }
            schalter.Loslassen();
            await WartenBis(async () => T((await a.Status())["stand"], "pruefsumme") == zweiter.Stand.Pruefsumme, Lang, "gleicher Modellstand");
            var fremde = strom.Alle(x => Art(x) == "aenderung" && T(x, "quelle") == "fremd");
            Assert.Equal(23, fremde.Sum(x => x["pakete"]?.GetValue<int>() ?? 1));
            Assert.Contains(fremde, x => x["pakete"]?.GetValue<int>() == 5);    // Etappe von 5 als ein Sammelereignis
            Assert.Contains(pakete[^1], fremde.Select(x => T(x, "paket")));
            Assert.All(fremde, x => Assert.Equal("fremd", T(x, "quelle")));
        }
        finally { schalter.Loslassen(); zweiter.Freigeben(); }
    }

    // ================================================================== Reservierungen, Schliessen, Beenden

    [Fact]
    public async Task Reservierung_ZweiterArbeitsplatzAbgelehnt_NachFreigebenMoeglich()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.03", ["name"] = "von pc1" });
            var res = (await a.Status())["reservierungen"]!.AsArray();
            Assert.Contains(res, r => T(r, "element") == "WC-E.03" && r!["eigene"]!.GetValue<bool>());

            var abgelehnt = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.03", ["name"] = "von pc2" });
            Assert.False(abgelehnt.Angenommen);
            Assert.NotEmpty(abgelehnt.Gesperrt);
            Assert.Contains("CS@pc1", abgelehnt.Fehler);

            var (code, frei) = await a.Post("/api/freigeben", new JsonObject());
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.True(frei!["freigegeben"]!.GetValue<bool>());
            Assert.DoesNotContain(Sperren(ordner), r => T(r, "geraet") == "pc1");

            var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.03", ["name"] = "von pc2" });
            Assert.True(erg.Angenommen, erg.Fehler);
            await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "Änderung von pc2 nach Freigabe");
            Assert.Equal("von pc2", T(await a.ElementAusModell("WC-E.03"), "name"));
            var res2 = (await a.Status())["reservierungen"]!.AsArray();
            Assert.Contains(res2, r => T(r, "element") == "WC-E.03" && !r!["eigene"]!.GetValue<bool>() && T(r, "geraet") == "pc2");
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task Schliessen_SchreibtStandabbildUndGibtReservierungenFrei()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1", e => e with { StandBeimSchliessen = 0 });
        var ordner = await a.Anlegen(temp.Projekt());
        var vorher = Staende(ordner);
        var paket = T(await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "vor dem Schliessen" }), "paketId")!;
        Assert.Contains(Sperren(ordner), r => T(r, "geraet") == "pc1");

        var (code, status) = await a.Post("/api/projekt/schliessen", new JsonObject());
        Assert.Equal(HttpStatusCode.OK, code);
        Assert.False(status!["offen"]!.GetValue<bool>());
        Assert.DoesNotContain(Sperren(ordner), r => T(r, "geraet") == "pc1");
        var nachher = Staende(ordner);
        var neu = nachher.Except(vorher).ToList();
        Assert.Single(neu);
        Assert.Contains(paket, StandPakete(ordner, neu[0]));
    }

    [Fact]
    public async Task Schliessen_UnterDerSchwelle_KeinNeuesStandabbild()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");   // Vorgabe: Standabbild erst ab 50 neuen Paketen
        var ordner = await a.Anlegen(temp.Projekt());
        var vorher = Staende(ordner);
        await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "eins" });
        await a.Post("/api/projekt/schliessen", new JsonObject());
        Assert.Equal(vorher, Staende(ordner));
        Assert.Empty(Sperren(ordner));
    }

    [Fact]
    public async Task ProzessEnde_GibtReservierungenFreiUndSchreibtStandabbild()
    {
        using var temp = new TempOrdner();
        var a = new Arbeitsplatz("pc1", e => e with { StandBeimSchliessen = 0 });
        string ordner;
        List<string> vorher;
        try
        {
            ordner = await a.Anlegen(temp.Projekt());
            vorher = Staende(ordner);
            await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "vor dem Beenden" });
            Assert.Contains(Sperren(ordner), r => T(r, "geraet") == "pc1");
        }
        finally
        {
            var uhr = Stopwatch.StartNew();
            await a.DisposeAsync();   // Server beenden: ApplicationStopping → Schliessen
            ausgabe.WriteLine($"Beenden: {uhr.Elapsed.TotalMilliseconds:0} ms");
        }
        Assert.DoesNotContain(Sperren(ordner), r => T(r, "geraet") == "pc1");
        Assert.Single(Staende(ordner).Except(vorher));
    }

    [Fact]
    public async Task Schliessen_BeiHaengenderAblage_KehrtNachZeitlimitZurueck()
    {
        using var temp = new TempOrdner();
        var schalter = new Schalter();
        await using var a = new Arbeitsplatz("pc1", e => e with
        {
            SpeicherFabrik = o => new SchaltbarerSpeicher(new OrdnerSpeicher(o), schalter),
            SchliessenZeitlimit = TimeSpan.FromSeconds(1),
            Wachhund = TimeSpan.FromSeconds(60),
        });
        var ordner = await a.Anlegen(temp.Projekt());
        await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "vor dem Hängen" });
        try
        {
            schalter.Anhalten();
            var uhr = Stopwatch.StartNew();
            var (code, status) = await a.Post("/api/projekt/schliessen", new JsonObject());
            var dauer = uhr.Elapsed;
            ausgabe.WriteLine($"Schliessen bei hängender Ablage: {dauer.TotalMilliseconds:0} ms");
            Assert.Equal(HttpStatusCode.OK, code);
            Assert.False(status!["offen"]!.GetValue<bool>());
            Assert.True(dauer < TimeSpan.FromSeconds(5), $"Schliessen hing {dauer.TotalMilliseconds:0} ms");
        }
        finally { schalter.Loslassen(); }
        // Kommt die Ablage zurück, wird die liegengebliebene Freigabe noch erledigt
        await WartenBis(() => Task.FromResult(!Sperren(ordner).Any(r => T(r, "geraet") == "pc1")), Lang, "Freigabe nach Rückkehr");
    }

    [Fact]
    public async Task RegelmaessigesStandabbild_NachVielenPaketen()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1", e => e with { StandNachPaketen = 3 });
        var ordner = await a.Anlegen(temp.Projekt());
        var vorher = Staende(ordner);
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            var pakete = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-1.03", ["name"] = $"Stand {i}" });
                Assert.True(erg.Angenommen, erg.Fehler);
                pakete.Add(erg.PaketId!);
            }
            await WartenBis(() => Task.FromResult(Staende(ordner).Except(vorher).Any(s => pakete.All(StandPakete(ordner, s).Contains))), Lang, "Standabbild mit den neuen Paketen");
            await WartenBis(async () => (await a.Status())["abgleich"]!["paketeSeitStandabbild"]?.GetValue<int>() == 0, Lang, "Zähler zurückgesetzt");
        }
        finally { zweiter.Freigeben(); }
    }

    // ================================================================== Projektwechsel, Öffnen, Ereignisstrom

    [Fact]
    public async Task Projektwechsel_WaechterWirdUmgehaengt()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner1 = await a.Anlegen(temp.Projekt("Projekt Eins"));
        var ordner2 = await a.Anlegen(temp.Projekt("Projekt Zwei"));
        var strom = await a.Verbinden();
        await WartenBis(async () => T((await a.Status())["abgleich"], "waechter") == Path.Combine(ordner2, "aenderungen"), Lang, "Wächter auf Projekt Zwei");

        var alt = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner1), "MB", "pc2");
        var neu = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner2), "MB", "pc2");
        try
        {
            var ergAlt = alt.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "im alten Projekt" });
            var ergNeu = neu.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "im neuen Projekt" });
            Assert.True(ergAlt.Angenommen && ergNeu.Angenommen);
            var (zeit, _) = await strom.Warten(x => T(x, "paket") == ergNeu.PaketId, Lang, "Änderung im neuen Projekt");
            Bericht("Fremde Änderung nach Projektwechsel", [zeit - PaketZeit(ergNeu.PaketId!)]);
            await Task.Delay(a.Einstellungen.Takt + TimeSpan.FromMilliseconds(500));
            Assert.Empty(strom.Alle(x => T(x, "paket") == ergAlt.PaketId));
            Assert.Equal("im neuen Projekt", T(await a.ElementAusModell("WC-E.02"), "name"));
        }
        finally { alt.Freigeben(); neu.Freigeben(); }
    }

    [Fact]
    public async Task Oeffnen_FehlerAendernNichtsUndLegenKeineOrdnerAn()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var (leer, j1) = await a.Post("/api/projekt/oeffnen", new JsonObject { ["ordner"] = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, leer);
        Assert.Contains("ordner", T(j1, "fehler"));
        var gibtsNicht = Path.Combine(temp.Wurzel, "gibt es nicht", "e_KICAD");
        var (nicht, j2) = await a.Post("/api/projekt/oeffnen", new JsonObject { ["ordner"] = gibtsNicht });
        Assert.Equal(HttpStatusCode.BadRequest, nicht);
        Assert.False(string.IsNullOrWhiteSpace(T(j2, "vorschlag")));
        Assert.False(Directory.Exists(gibtsNicht), "Öffnen hat den Ordner angelegt");
        var status = await a.Status();
        Assert.True(status["offen"]!.GetValue<bool>());
        Assert.Equal(ordner, T(status, "ort"));
    }

    [Fact]
    public async Task Ereignisstrom_AbbruchDesBrowsers_RaeumtKanalAb()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        await a.Anlegen(temp.Projekt());
        var s1 = await a.Verbinden();
        var s2 = await a.Verbinden();
        Assert.Equal(2, a.Dienst.Zuhoerer);
        await s1.DisposeAsync();
        await WartenBis(() => Task.FromResult(a.Dienst.Zuhoerer == 1), Lang, "Kanal abgeräumt");
        // der verbleibende Strom bekommt weiter alles
        var paket = T(await a.Befehl("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "nach Abbruch" }), "paketId");
        await s2.Warten(x => T(x, "paket") == paket, Lang, "Ereignis im verbleibenden Strom");
    }

    [Fact]
    public async Task Ereignisstrom_Ueberlauf_ErsetztPufferDurchNeuLaden()
    {
        var dienst = new ProjektDienst(new Einstellungen("CS", "pc1", Katalog.StandardOrdner(), null) { EreignisPuffer = 10, ZuletztMerken = false },
            NullLogger<ProjektDienst>.Instance);
        using var ende = new CancellationTokenSource();
        var leser = dienst.Abonnieren(ende.Token);
        for (int i = 0; i < 25; i++) dienst.Senden(new JsonObject { ["art"] = "test", ["n"] = i });
        var erhalten = new List<JsonObject>();
        while (leser.TryRead(out var text)) erhalten.Add(JsonNode.Parse(text)!.AsObject());
        Assert.True(erhalten.Count <= 11, $"{erhalten.Count} Ereignisse im Puffer");
        Assert.Equal("projekt", T(erhalten[0], "art"));
        Assert.True(erhalten[0]["neuLaden"]!.GetValue<bool>());
        var nummern = erhalten.Skip(1).Select(x => x["n"]!.GetValue<int>()).ToList();
        Assert.Equal(24, nummern[^1]);
        Assert.Equal(Enumerable.Range(nummern[0], nummern.Count), nummern);   // nach «neu laden» lückenlos
        ende.Cancel();
        Assert.Equal(0, dienst.Zuhoerer);
        Assert.False(await leser.WaitToReadAsync());   // Kanal abgeschlossen
    }

    [Fact]
    public async Task Ereignisstrom_LeserDerNichtMehrLiest_WirdBeimUeberlaufAbgemeldet()
    {
        var dienst = new ProjektDienst(new Einstellungen("CS", "pc1", Katalog.StandardOrdner(), null)
            { EreignisPuffer = 5, EreignisLeserTotNach = TimeSpan.FromMilliseconds(100), ZuletztMerken = false }, NullLogger<ProjektDienst>.Instance);
        using var ende = new CancellationTokenSource();
        var tot = dienst.Abonnieren(ende.Token);
        var lebt = dienst.Abonnieren(ende.Token);
        await Task.Delay(300);
        for (int i = 0; i < 12; i++)
        {
            dienst.Senden(new JsonObject { ["art"] = "test", ["n"] = i });
            while (lebt.TryRead(out _)) { }
        }
        Assert.Equal(1, dienst.Zuhoerer);
        while (tot.TryRead(out _)) { }
        Assert.False(await tot.WaitToReadAsync());   // Kanal des toten Fensters abgeschlossen
    }

    [Fact]
    public async Task Waechterfehler_WirdNeuAufgesetzt()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var dienst = a.Abgleich;
        var feld = typeof(AbgleichDienst).GetField("waechter", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FileSystemWatcher? alt = null;
        await WartenBis(() => Task.FromResult((alt = feld.GetValue(dienst) as FileSystemWatcher) is not null), Lang, "Dateiwächter aufgesetzt");

        // Windows meldet «Puffer übergelaufen» bzw. Netzfehler über das Error-Ereignis
        typeof(FileSystemWatcher).GetMethod("OnError", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(alt, [new ErrorEventArgs(new InternalBufferOverflowException("nachgestellt"))]);
        await WartenBis(() => Task.FromResult(feld.GetValue(dienst) is FileSystemWatcher neu && !ReferenceEquals(neu, alt)), Lang, "neuer Dateiwächter");

        // der neue Wächter meldet die nächste fremde Änderung
        int Meldungen(JsonObject s) => s["abgleich"]!["waechterMeldungen"]!.GetValue<int>();
        var vorher = Meldungen(await a.Status());
        var zweiter = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        try
        {
            var erg = zweiter.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "nach Wächterfehler" });
            Assert.True(erg.Angenommen, erg.Fehler);
            var (zeit, _) = await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "fremde Änderung nach Wächterfehler");
            Bericht("Fremde Änderung nach Wächterfehler", [zeit - PaketZeit(erg.PaketId!)]);
            await WartenBis(async () => Meldungen(await a.Status()) > vorher, Lang, "Meldung des neuen Wächters");
        }
        finally { zweiter.Freigeben(); }
    }

    [Fact]
    public async Task UngueltigerPaketname_BlockiertWederOeffnenNochAbgleich()
    {
        using var temp = new TempOrdner();
        await using var a = new Arbeitsplatz("pc1");
        var ordner = await a.Anlegen(temp.Projekt());
        var strom = await a.Verbinden();
        var p2 = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");   // vor der Datei öffnen (Befund unten)
        // Dateiname, den OrdnerSpeicher nicht als Paket-Id annimmt (länger als 200 Zeichen)
        var lang = "20000101T000000000Z_" + new string('z', 190);
        await File.WriteAllTextAsync(Path.Combine(ordner, "aenderungen", lang + ".json"), "{}");
        try
        {
            var erg = p2.Ausfuehren("element_aendern", new JsonObject { ["element"] = "WC-E.02", ["name"] = "trotz langem Namen" });
            Assert.True(erg.Angenommen, erg.Fehler);
            await strom.Warten(x => T(x, "paket") == erg.PaketId, Lang, "fremde Änderung trotz ungültigem Namen");
            await strom.Warten(x => Art(x) == "abgleich" && (T(x, "fehler") ?? "").Contains("zzzz"), Lang, "Meldung ungültiger Name");
            // erneut öffnen geht (die Hülle meldet den Namen als unlesbar statt abzubrechen)
            var (code, j) = await a.Post("/api/projekt/oeffnen", new JsonObject { ["ordner"] = ordner });
            Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
        }
        finally { p2.Freigeben(); }
    }

    // ------------------------------------------------------------------ Befunde am Kern (behoben 17.10.2026)

    [Fact]
    public void Kern_SynchronisierenUeberspringtUngueltigenPaketnamen()
    {
        using var temp = new TempOrdner();
        var ordner = temp.Projekt();
        Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(ordner)).Freigeben();
        File.WriteAllText(Path.Combine(ordner, "aenderungen", "20000101T000000000Z_" + new string('z', 190) + ".json"), "{}");
        // erwartet: Paket als unlesbar melden (SyncErgebnis.Fehler) und weitermachen – nicht ArgumentException
        var p = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "MB", "pc2");
        Assert.Contains(p.Synchronisieren().Fehler, f => f.Contains("zzzz"));
    }

    [Fact]
    public void Kern_OrdnerSpeicherLegtNichtVorhandenenOrdnerNichtAn()
    {
        using var temp = new TempOrdner();
        var gibtsNicht = Path.Combine(temp.Wurzel, "vertippt", "e_KICAD");
        try { _ = new OrdnerSpeicher(gibtsNicht).KopfLesen(); } catch (Exception) { /* Fehler ist in Ordnung */ }
        Assert.False(Directory.Exists(gibtsNicht), "OrdnerSpeicher hat den nicht vorhandenen Ordner samt Unterordnern angelegt");
    }

    [Fact]
    public void Kern_BeispielprojektGibtReservierungenFrei()
    {
        using var temp = new TempOrdner();
        var ordner = temp.Projekt();
        Beispielprojekt.Erzeugen(Kat, new OrdnerSpeicher(ordner), "pc1");
        Assert.Empty(Sperren(ordner));
    }

    // ================================================================== Hilfen

    static string? Art(JsonNode? n) => T(n, "art");
    static string? T(JsonNode? n, string name) => n?[name] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
    static List<string> Ids(JsonNode n) => n["ids"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();

    /// <summary>Zeit eines Pakets aus seiner Id (UTC, ms) – gesetzt unmittelbar bevor das Paket geschrieben wird.</summary>
    static DateTime PaketZeit(string id) =>
        DateTime.ParseExact(id[..19], "yyyyMMdd'T'HHmmssfff'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    void Bericht(string was, List<TimeSpan> zeiten) =>
        ausgabe.WriteLine($"{was}: {string.Join(", ", zeiten.Select(z => $"{z.TotalMilliseconds:0} ms"))} (Abnahme-Grenze {Abnahme.TotalMilliseconds:0} ms)");

    /// <summary>Reservierungsdateien; eine Datei, die gerade freigegeben (gelöscht) wird, zählt nicht.</summary>
    static List<JsonObject> Sperren(string ordner) =>
        Directory.GetFiles(Path.Combine(ordner, "sperren"), "*.json").Where(f => !Path.GetFileName(f).StartsWith('.'))
            .Select(f => { try { return JsonNode.Parse(File.ReadAllText(f)) as JsonObject; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return null; } })
            .Where(o => o is not null).Select(o => o!).ToList();

    static List<string> Staende(string ordner) =>
        Directory.GetFiles(Path.Combine(ordner, "stand"), "stand-*.json").Select(Path.GetFileName).Select(n => n!).Order(StringComparer.Ordinal).ToList();

    /// <summary>Pakete eines Standabbilds; ein inzwischen aufgeräumtes (ältere als die drei neuesten) gilt als leer.</summary>
    static List<string> StandPakete(string ordner, string datei)
    {
        try { return JsonNode.Parse(File.ReadAllText(Path.Combine(ordner, "stand", datei)))!["enthaltenePakete"]!.AsArray().Select(x => x!.GetValue<string>()).ToList(); }
        catch (IOException) { return []; }
    }

    static async Task WartenBis(Func<Task<bool>> bedingung, TimeSpan zeit, string was)
    {
        var bis = DateTime.UtcNow + zeit;
        while (!await bedingung())
        {
            if (DateTime.UtcNow > bis) throw new Xunit.Sdk.XunitException($"«{was}» nicht innert {zeit.TotalSeconds:0} s.");
            await Task.Delay(50);
        }
    }

    static async Task Umbenennen(string von, string nach)
    {
        for (int i = 0; ; i++)
        {
            try { Directory.Move(von, nach); return; }
            catch (Exception) when (i < 50) { await Task.Delay(100); }
        }
    }

    // ------------------------------------------------------------------ Kommandozeile (eigener Prozess)

    static readonly Lazy<string> McpDllPfad = new(() => McpDllFinden(), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Gebaute Hlks.Mcp.dll im selben Bauordner (HLKS_BUILD); fehlt sie oder ist sie älter als der Kern der
    /// Tests, wird Hlks.Mcp gebaut.</summary>
    static string McpDllFinden([CallerFilePath] string quelle = "")
    {
        var tfm = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd('\\', '/'));    // …\Hlks.Web.Tests\bin\<Konfig>\<tfm>
        var konfig = tfm.Parent!;
        var basis = konfig.Parent!.Parent!.Parent!;
        var dll = Path.Combine(basis.FullName, "Hlks.Mcp", "bin", konfig.Name, tfm.Name, "Hlks.Mcp.dll");
        var kernHier = Path.Combine(AppContext.BaseDirectory, "Hlks.Kern.dll");
        var kernDort = Path.Combine(Path.GetDirectoryName(dll)!, "Hlks.Kern.dll");
        bool veraltet = !File.Exists(dll) || !File.Exists(kernDort) || File.GetLastWriteTimeUtc(kernDort) < File.GetLastWriteTimeUtc(kernHier);
        if (veraltet)
        {
            var wurzel = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(quelle)!, "..", ".."));
            var psi = new ProcessStartInfo(Dotnet) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var x in new[] { "build", Path.Combine(wurzel, "src", "Hlks.Mcp", "Hlks.Mcp.csproj"), "-c", konfig.Name, "-nologo", "-v", "q" }) psi.ArgumentList.Add(x);
            psi.Environment["HLKS_BUILD"] = basis.FullName;
            using var p = Process.Start(psi)!;
            var aus = p.StandardOutput.ReadToEndAsync();
            var fehler = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(600_000) || p.ExitCode != 0) throw new InvalidOperationException("Hlks.Mcp bauen fehlgeschlagen: " + aus.Result + fehler.Result);
        }
        return File.Exists(dll) ? dll : throw new FileNotFoundException("Hlks.Mcp.dll nicht gefunden", dll);
    }

    static async Task<(int Code, string Aus, string Fehler)> Cli(params string[] args)
    {
        var psi = new ProcessStartInfo(Dotnet)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, UseShellExecute = false, CreateNoWindow = true,
        };
        psi.ArgumentList.Add(McpDllPfad.Value);
        foreach (var x in args) psi.ArgumentList.Add(x);
        psi.Environment["HLKS_KATALOG"] = Katalog.StandardOrdner();
        using var p = Process.Start(psi)!;
        p.StandardInput.Close();
        var aus = p.StandardOutput.ReadToEndAsync();
        var fehler = p.StandardError.ReadToEndAsync();
        using var zeit = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await p.WaitForExitAsync(zeit.Token); }
        catch (OperationCanceledException) { p.Kill(true); throw new TimeoutException("Hlks.Mcp " + string.Join(' ', args)); }
        return (p.ExitCode, await aus, await fehler);
    }

    // ------------------------------------------------------------------ Arbeitsplatz, Ereignisstrom, Ordner

    /// <summary>Ein Arbeitsplatz: eigener Web-Server (TestServer) mit eigenem Akteur/Gerät und eigenen Einstellungen.</summary>
    sealed class Arbeitsplatz : IAsyncDisposable
    {
        readonly WebApplicationFactory<Program> basis = new();
        readonly WebApplicationFactory<Program> fabrik;
        readonly List<Ereignisstrom> stroeme = [];
        bool entsorgt;

        public Einstellungen Einstellungen { get; }
        public HttpClient Http { get; }
        public ProjektDienst Dienst => fabrik.Services.GetRequiredService<ProjektDienst>();
        public AbgleichDienst Abgleich => fabrik.Services.GetServices<IHostedService>().OfType<AbgleichDienst>().Single();

        public Arbeitsplatz(string geraet, Func<Einstellungen, Einstellungen>? anpassen = null, string akteur = "CS")
        {
            var e = new Einstellungen(akteur, geraet, Katalog.StandardOrdner(), null) { ZuletztMerken = false };
            Einstellungen = anpassen?.Invoke(e) ?? e;
            fabrik = basis.WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton(Einstellungen)));
            Http = fabrik.CreateClient();
            Http.Timeout = TimeSpan.FromSeconds(90);
        }

        public async Task<string> Anlegen(string ordner)
        {
            var (code, j) = await Post("/api/projekt/anlegen", new JsonObject { ["ordner"] = ordner, ["name"] = "Abgleich", ["beispiel"] = true });
            Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
            Assert.True(j!["offen"]!.GetValue<bool>());
            return ordner;
        }

        public async Task<(HttpStatusCode Code, JsonNode? Json)> Post(string pfad, JsonNode koerper)
        {
            using var antwort = await Http.PostAsync(pfad, new StringContent(koerper.ToJsonString(), Encoding.UTF8, "application/json"));
            var text = await antwort.Content.ReadAsStringAsync();
            return (antwort.StatusCode, text.Length == 0 ? null : JsonNode.Parse(text));
        }

        public async Task<JsonNode> Befehl(string name, JsonObject par)
        {
            var (code, j) = await Post("/api/befehl/" + name, par);
            Assert.True(code == HttpStatusCode.OK, j?.ToJsonString());
            Assert.True(j!["angenommen"]!.GetValue<bool>(), j.ToJsonString());
            return j;
        }

        public async Task<JsonObject> Status() => JsonNode.Parse(await Http.GetStringAsync("/api/status"))!.AsObject();

        public async Task<JsonNode?> ElementAusModell(string kennzeichen)
        {
            var m = JsonNode.Parse(await Http.GetStringAsync("/api/modell"))!;
            return m["elemente"]!.AsArray().FirstOrDefault(e => T(e, "kennzeichen") == kennzeichen);
        }

        public async Task<string> ElementId(string kennzeichen) => T(await ElementAusModell(kennzeichen), "id")!;

        public async Task<Ereignisstrom> Verbinden()
        {
            var s = await Ereignisstrom.Verbinden(fabrik.CreateClient());
            stroeme.Add(s);
            return s;
        }

        public async ValueTask DisposeAsync()
        {
            if (entsorgt) return;
            entsorgt = true;
            foreach (var s in stroeme) await s.DisposeAsync();
            Http.Dispose();
            await basis.DisposeAsync();   // beendet den Server (auch die abgeleitete Fabrik)
        }
    }

    /// <summary>Liest /api/ereignisse wie der Browser (Server-Sent Events) und merkt sich jedes Ereignis mit Ankunftszeit.</summary>
    sealed class Ereignisstrom : IAsyncDisposable
    {
        readonly List<(DateTime Zeit, JsonObject E)> liste = [];
        readonly CancellationTokenSource ende = new();
        readonly TaskCompletionSource hallo = new(TaskCreationOptions.RunContinuationsAsynchronously);
        HttpClient? http;
        HttpResponseMessage? antwort;
        Task? leser;

        public static async Task<Ereignisstrom> Verbinden(HttpClient http)
        {
            var s = new Ereignisstrom { http = http };
            http.Timeout = Timeout.InfiniteTimeSpan;
            s.antwort = await http.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/api/ereignisse"), HttpCompletionOption.ResponseHeadersRead, s.ende.Token);
            s.antwort.EnsureSuccessStatusCode();
            var strom = await s.antwort.Content.ReadAsStreamAsync(s.ende.Token);
            s.leser = Task.Run(() => s.Lesen(strom));
            await s.hallo.Task.WaitAsync(TimeSpan.FromSeconds(30));
            return s;
        }

        async Task Lesen(Stream strom)
        {
            try
            {
                using var r = new StreamReader(strom, Encoding.UTF8);
                string? art = null;
                while (await r.ReadLineAsync(ende.Token) is { } zeile)
                {
                    if (zeile.StartsWith("event: ", StringComparison.Ordinal)) art = zeile[7..];
                    else if (zeile.StartsWith("data: ", StringComparison.Ordinal))
                    {
                        if (art == "hallo") hallo.TrySetResult();
                        else if (JsonNode.Parse(zeile[6..]) is JsonObject o) lock (liste) liste.Add((DateTime.UtcNow, o));
                        art = null;
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException) { }
        }

        public List<JsonObject> Alle(Func<JsonObject, bool>? bedingung = null)
        {
            lock (liste) return liste.Select(x => x.E).Where(e => bedingung?.Invoke(e) ?? true).ToList();
        }

        public async Task<(DateTime Zeit, JsonObject E)> Warten(Func<JsonObject, bool> bedingung, TimeSpan zeit, string was)
        {
            var bis = DateTime.UtcNow + zeit;
            while (true)
            {
                lock (liste) foreach (var x in liste) if (bedingung(x.E)) return x;
                if (DateTime.UtcNow > bis)
                    throw new Xunit.Sdk.XunitException($"Ereignis «{was}» nicht innert {zeit.TotalSeconds:0} s. Erhalten:\n"
                        + string.Join("\n", Alle().Select(e => { var k = (JsonObject)e.DeepClone(); k.Remove("stand"); return k.ToJsonString(); })));
                await Task.Delay(20);
            }
        }

        public async ValueTask DisposeAsync()
        {
            ende.Cancel();
            antwort?.Dispose();
            if (leser is not null) await Task.WhenAny(leser, Task.Delay(5000));
            http?.Dispose();
        }
    }

    /// <summary>Temp-Ordner im Stil der NAS-Ablage (Umlaut, Leerzeichen); wird am Ende gelöscht.</summary>
    sealed class TempOrdner : IDisposable
    {
        public string Wurzel { get; } = Path.Combine(Path.GetTempPath(), "hlks_abgleich_" + Guid.NewGuid().ToString("N")[..10]);
        public string Projekt(string name = "Projekt Ä") => Path.Combine(Wurzel, name, "c_Pläne und Modelle", "e_KICAD");

        public void Dispose()
        {
            for (int i = 0; i < 10; i++)
            {
                try { if (Directory.Exists(Wurzel)) Directory.Delete(Wurzel, true); return; }
                catch (IOException) { Thread.Sleep(200); }
                catch (UnauthorizedAccessException) { Thread.Sleep(200); }
            }
        }
    }

    /// <summary>Stellt Netzausfälle nach: Weg = jeder Zugriff wirft IOException (wie SMB ohne Netz), Anhalten = jeder
    /// Zugriff wartet, bis Loslassen (wie ein hängender SMB-Zugriff).</summary>
    sealed class Schalter
    {
        public volatile bool Weg;
        volatile bool haengen;
        readonly ManualResetEventSlim frei = new(true);
        public void Anhalten() { frei.Reset(); haengen = true; }
        public void Loslassen() { haengen = false; frei.Set(); }
        public void Tor()
        {
            if (Weg) throw new IOException("Der Netzwerkpfad wurde nicht gefunden (nachgestellt).");
            if (haengen) frei.Wait(TimeSpan.FromSeconds(60));
        }
    }

    sealed class SchaltbarerSpeicher(IProjektSpeicher innen, Schalter schalter) : IProjektSpeicher
    {
        public string Ort => innen.Ort;
        public void Anlegen() { schalter.Tor(); innen.Anlegen(); }
        public ProjektKopf? KopfLesen() { schalter.Tor(); return innen.KopfLesen(); }
        public void KopfSchreiben(ProjektKopf kopf) { schalter.Tor(); innen.KopfSchreiben(kopf); }
        public StandAbbild? StandLesen() { schalter.Tor(); return innen.StandLesen(); }
        public void StandSchreiben(StandAbbild stand) { schalter.Tor(); innen.StandSchreiben(stand); }
        public IReadOnlyList<string> PaketListe() { schalter.Tor(); return innen.PaketListe(); }
        public Paket PaketLesen(string id) { schalter.Tor(); return innen.PaketLesen(id); }
        public void PaketSchreiben(Paket paket) { schalter.Tor(); innen.PaketSchreiben(paket); }
        public IReadOnlyList<Reservierung> Reservieren(IEnumerable<string> elementIds, string akteur, string geraet) { schalter.Tor(); return innen.Reservieren(elementIds, akteur, geraet); }
        public void Freigeben(string akteur, string geraet, IEnumerable<string>? elementIds = null) { schalter.Tor(); innen.Freigeben(akteur, geraet, elementIds); }
        public IReadOnlyList<Reservierung> Reservierungen() { schalter.Tor(); return innen.Reservierungen(); }
        public string? UnterlagenOrdner => innen.UnterlagenOrdner;
    }
}
