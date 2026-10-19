using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using static Hlks.Kern.Tests.ArchitekturHilfe;

namespace Hlks.Kern.Tests;

/// <summary>Zwischengespeicherte Prüfsumme des Modells (Modul 2, B1, Entscheid Hauptlauf 18.10.2026): gleicher Wert wie die
/// bisherige Rechnung, verworfen bei jedem Anwenden und Laden, direkte Änderungen werden beim Nachprüfen erkannt, und ein
/// Arbeitsablauf über Befehle, Rückgängig, Abgleich zwischen zwei Arbeitsplätzen und Öffnen ändert nichts am Modell vorbei.</summary>
public sealed class PruefsummeTests(ITestOutputHelper aus) : IDisposable
{
    readonly string wurzel = Path.Combine(Path.GetTempPath(), "hlks-pruefsumme-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        for (int i = 0; i < 3 && Directory.Exists(wurzel); i++)
        {
            try { Directory.Delete(wurzel, true); }
            catch (IOException) { Thread.Sleep(100); }
            catch (UnauthorizedAccessException) { Thread.Sleep(100); }
        }
    }

    /// <summary>Die Rechnung vor dem Zwischenspeicher (Modell.cs bis 18.10.2026), unverändert übernommen.</summary>
    static string Bisher(Modell m)
    {
        var sb = new StringBuilder();
        foreach (var e in m.Elemente.OrderBy(e => e.Id, StringComparer.Ordinal))
        {
            var k = e.Kopie(); k.Version = 0; k.Paket = null;
            sb.Append(Json.Text(k)).Append('\n');
        }
        foreach (var v in m.Verbindungen.OrderBy(v => v.Id, StringComparer.Ordinal))
        {
            var k = v.Kopie(); k.Version = 0; k.Paket = null;
            sb.Append(Json.Text(k)).Append('\n');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..12].ToLowerInvariant();
    }

    [Fact]
    public void GleicherWertWieBisher()
    {
        var b = Beispielprojekt.Erzeugen(Kat, null);
        Assert.NotEmpty(b.Modell.Verbindungen);
        Assert.Equal(Bisher(b.Modell), b.Modell.Pruefsumme());
        var f = FamilieTestHilfe.MitHeizkoerper(parameter: new JsonObject { ["bodenabstand"] = 200 });
        Assert.Equal(Bisher(f.Modell), f.Modell.Pruefsumme());
        Assert.Equal(Bisher(new Modell()), new Modell().Pruefsumme());
    }

    [Fact]
    public void ZwischenspeicherVerworfenBeiAnwendenUndLaden()
    {
        var p = Neu();
        var a = p.Modell.Pruefsumme();
        var zaehler = p.Modell.Aenderungszaehler;
        Assert.Equal(a, p.Modell.Pruefsumme());   // ohne Änderung: gespeicherter Wert, Zähler unverändert
        Assert.Equal(zaehler, p.Modell.Aenderungszaehler);
        Ok(p.Ausfuehren("element_aendern", new JsonObject { ["element"] = "EG", ["name"] = "Erdgeschoss" }));
        Assert.True(p.Modell.Aenderungszaehler > zaehler);
        var b = p.Modell.Pruefsumme();
        Assert.NotEqual(a, b);
        Assert.Equal(Bisher(p.Modell), b);
        Ok(p.Rueckgaengig());
        Assert.Equal(a, p.Modell.Pruefsumme());   // gleicher Inhalt → gleiche Prüfsumme
        Ok(p.Wiederholen());
        Assert.Equal(b, p.Modell.Pruefsumme());
        // Kopie: gleicher Inhalt; Laden eines anderen Stands verwirft
        Assert.Equal(b, p.Modell.Kopie().Pruefsumme());
        var leer = new Modell(); leer.Pruefsumme();
        leer.Laden(p.Modell.Abbild([]));
        Assert.Equal(b, leer.Pruefsumme());
        Assert.Equal(b, p.Stand.Pruefsumme);
    }

    [Fact]
    public void DirekteAenderungWirdBeimNachpruefenErkannt()
    {
        var p = Neu();
        using (Modell.PruefsummeNachpruefen())
        {
            p.Modell.Pruefsumme();
            Hole(p, "EG").Name = "direkt geändert";   // am Modell vorbei (gegen die Regel)
            var f = Assert.Throws<InvalidOperationException>(() => p.Modell.Pruefsumme());
            Assert.Contains("ohne Anwenden geändert", f.Message);
        }
        // ohne Nachprüfen bleibt der gespeicherte Wert – darum gilt die Regel «nur über Operationen ändern»
        // (im Suchlauf mit HLKS_PRUEFSUMME_NACHPRUEFEN=1 wird immer nachgerechnet)
        if (Environment.GetEnvironmentVariable("HLKS_PRUEFSUMME_NACHPRUEFEN") != "1") Assert.NotEqual(Bisher(p.Modell), p.Modell.Pruefsumme());
    }

    [Fact]
    public void ArbeitsablaufAendertNichtsAmModellVorbei()
    {
        var ordner = Path.Combine(wurzel, "Projekt Müller", "e_KICAD");
        using var _ = Modell.PruefsummeNachpruefen();
        var a = Projekt.Anlegen(Kat, new OrdnerSpeicher(ordner), "Test Prüfsumme", "A", "pc-a");
        var b = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "B", "pc-b");
        void Gleich() { a.Synchronisieren(); b.Synchronisieren(); Assert.Equal(a.Modell.Pruefsumme(), b.Modell.Pruefsumme()); Assert.Equal(Bisher(a.Modell), a.Modell.Pruefsumme()); }

        Ok(a.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Gebaeude", ["kennzeichen"] = "GEB" }));
        Ok(a.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Geschoss", ["kennzeichen"] = "EG", ["teilVon"] = "GEB", ["eigenschaften"] = new JsonObject { ["kote"] = 0, ["hoehe"] = 3000 } }));
        Ok(a.Ausfuehren("element_anlegen", new JsonObject { ["klasse"] = "Ansicht", ["kennzeichen"] = "AN-EG", ["geschoss"] = "EG", ["eigenschaften"] = new JsonObject { ["art"] = "grundriss", ["massstab"] = 50 } }));
        Gleich();
        Ok(b.Ausfuehren("wand_zeichnen", new JsonObject { ["geschoss"] = "EG", ["punkte"] = Pts([0, 0], [6000, 0], [6000, 4000], [0, 4000]), ["dicke"] = 200, ["geschlossen"] = true }));
        Ok(b.Ausfuehren("familie_entwurf", new JsonObject { ["definition"] = FamilieTestHilfe.Testheizkoerper() }));
        Ok(b.Ausfuehren("familie_platzieren", new JsonObject { ["familie"] = "FAM-1", ["typ"] = "22-600-1000", ["punkt"] = new JsonArray(3000, 200, 0), ["drehung"] = 0, ["kennzeichen"] = "HK-1" }));
        Gleich();
        Ok(a.Ausfuehren("familie_aendern", new JsonObject { ["familie"] = "FAM-1", ["ops"] = new JsonArray(new JsonObject { ["op"] = "anschluss_loeschen", ["name"] = "rl" }) }));
        Ok(a.Ausfuehren("element_aendern", new JsonObject { ["element"] = "HK-1", ["eigenschaften"] = new JsonObject { ["familientyp"] = "22-900-800" } }));
        Gleich();
        Ok(a.Rueckgaengig()); Ok(a.Rueckgaengig()); Ok(a.Wiederholen());
        Gleich();
        // Lesen ändert nichts: Grafik, Abfragen, Darstellung
        Grafik.Plan(a, "AN-EG"); Grafik.Koerper(a); Tuer.Abfrage(a, "projekt_ueberblick"); Tuer.Abfrage(a, "familie", new JsonObject { ["familie"] = "FAM-1" });
        Tuer.Abfrage(a, "anschlusslagen", new JsonObject { ["element"] = "HK-1" }); Darstellung.Modell(a); Pruefungen.Alle(a.Modell, a.Katalog);
        Gleich();
        // Öffnen auf einem dritten Arbeitsplatz (Laden + Synchronisieren)
        a.StandSichern();
        var c = Projekt.Oeffnen(Kat, new OrdnerSpeicher(ordner), "C", "pc-c");
        Assert.Equal(a.Modell.Pruefsumme(), c.Modell.Pruefsumme());
    }

    [Fact]
    public void GrundrissMitFamilienRechnetDiePruefsummeNurNachAenderungen()
    {
        var p = FamilieTestHilfe.MitHeizkoerper();
        Grafik.Plan(p, "AN-EG");
        var zaehler = p.Modell.Aenderungszaehler;
        var uhr = Stopwatch.StartNew();
        for (int i = 0; i < 3; i++) Grafik.Plan(p, "AN-EG");
        aus.WriteLine($"3 Grundrisse ohne Änderung: {uhr.Elapsed.TotalMilliseconds:0} ms");
        Assert.Equal(zaehler, p.Modell.Aenderungszaehler);   // Lesen zählt nicht
        Assert.Equal(p.Modell.Pruefsumme(), Grafik.Plan(p, "AN-EG").Stand);
    }
}
