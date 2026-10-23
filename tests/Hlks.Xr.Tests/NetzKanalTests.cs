// Quest App P4a: Regeln und Nachrichten des Live-Kanals (KanalRegeln, PosenTakt, KanalText) und die Umrechnung App ↔ KICAD (KicadUmrechnung).
// Die Verbindung selbst (TLS mit Pinning, hallo/willkommen, Posen, Auftrag, 4001) prüft der Integrationstest gegen den echten Server.

using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hlks.Xr.Netz;
using static Hlks.Xr.Tests.T;

namespace Hlks.Xr.Tests;

public class NetzKanalTests
{
    // ---------------------------------------------------------------------------------------------- Regeln

    [Fact]
    public void Wiederverbinden_mit_wachsender_Wartezeit_nicht_nach_4001_und_4002()
    {
        Assert.Equal([1.0, 2, 4, 8, 15, 30, 30, 30], Enumerable.Range(0, 8).Select(i => KanalRegeln.Wartezeit(i).TotalSeconds));
        Assert.Equal(1, KanalRegeln.Wartezeit(-3).TotalSeconds);
        Assert.False(KanalRegeln.NeuVerbinden(4001));   // Kopplung widerrufen
        Assert.False(KanalRegeln.NeuVerbinden(4002));   // durch eine neue Verbindung derselben Brille ersetzt
        foreach (var c in new int?[] { null, 1000, 1001, 1006, 1008, 1011, 1013, 4003 }) Assert.True(KanalRegeln.NeuVerbinden(c), $"{c}");
        Assert.Equal(KanalZustand.Widerrufen, KanalRegeln.ZustandNach(4001));
        Assert.Equal(KanalZustand.Ersetzt, KanalRegeln.ZustandNach(4002));
        Assert.Equal(KanalZustand.Wartet, KanalRegeln.ZustandNach(1001));
    }

    [Fact]
    public void Posen_im_Takt_25_je_Sekunde_auch_bei_72_und_90_Hz()
    {
        foreach (var hz in new[] { 72.0, 90, 120 })
        {
            var t = new PosenTakt();
            int n = 0;
            for (int i = 0; i < hz * 4; i++) if (t.Faellig(i / hz)) n++;
            Assert.InRange(n / 4.0, 20, 25.5);   // Auftrag: 20–30 je Sekunde
        }
        var schnell = new PosenTakt(100);   // höchstens 30 (Grenze des Servers)
        Assert.InRange(Enumerable.Range(0, 1000).Count(i => schnell.Faellig(i / 1000.0)), 29, 31);
        var p = new PosenTakt(25);
        Assert.True(p.Faellig(0));
        Assert.False(p.Faellig(0.01));
        Assert.True(p.Faellig(10));        // nach einer Pause sofort
        Assert.False(p.Faellig(10.02));    // und nicht aufholen
    }

    // ---------------------------------------------------------------------------------------------- Nachrichten

    [Fact]
    public void Hallo_und_Tschuess_nach_dem_Schema_des_Servers()
    {
        var o = JsonNode.Parse(KanalText.Hallo("  Quest Christian\n", "Meta Quest 3", "0.1 (P4a)"))!;
        Assert.Equal("hallo", (string)o["typ"]!);
        Assert.Equal(1, (int)o["fassung"]!);
        Assert.Equal("Quest Christian", (string)o["name"]!);   // Steuerzeichen und Rand weg
        Assert.Equal("Meta Quest 3", (string)o["geraet"]!);
        Assert.Equal("0.1 (P4a)", (string)o["app"]!);
        var lang = JsonNode.Parse(KanalText.Hallo(new string('x', 99), "", null))!;
        Assert.Equal(40, ((string)lang["name"]!).Length);       // Grenze des Servers: 40
        Assert.Equal("?", (string)lang["geraet"]!);             // leer ist am Server ungültig
        Assert.Null(lang["app"]);
        Assert.Equal("{\"typ\":\"tschuess\",\"fassung\":1}", KanalText.Tschuess());
    }

    [Fact]
    public void Pose_in_KICAD_mm_gerundet_mit_Haenden_Modus_Massstab()
    {
        var kopf = new KicadLage([1234.56789, -2000.04, 1700], [0.000001, 0.7071068, 0, 0.7071068]);
        var hand = new KicadHand(new KicadLage([1000, 2000, 1200], [0, 0, 0, 1]), [1050.123, 2100, 1250], true, 0.87654);
        var o = JsonNode.Parse(KanalText.Pose(new KicadPose(kopf, hand, null)))!;
        Assert.Equal("pose", (string)o["typ"]!);
        Assert.Equal([1234.6, -2000, 1700], o["kopf"]!["pos"]!.AsArray().Select(x => (double)x!));
        Assert.Equal([0, 0.70711, 0, 0.70711], o["kopf"]!["rot"]!.AsArray().Select(x => (double)x!));
        Assert.Equal([1050.1, 2100, 1250], o["links"]!["zeigefinger"]!.AsArray().Select(x => (double)x!));
        Assert.True((bool)o["links"]!["pinch"]!);
        Assert.Equal(0.877, (double)o["links"]!["pinchStaerke"]!);
        Assert.Null(o["rechts"]);
        Assert.Equal("begehen", (string)o["modus"]!);
        Assert.Equal(1, (double)o["massstab"]!);
        Assert.Equal([0.0, 0, 0], JsonNode.Parse(KanalText.Pose(new KicadPose(new KicadLage([double.NaN, 0, 0], [0, 0, 0, 1]), null, null)))!["kopf"]!["pos"]!.AsArray().Select(x => (double)x!));
        var z = JsonNode.Parse(KanalText.Zeigen("R-1.04", [1, 2, 3]))!;
        Assert.Equal("zeigen", (string)z["typ"]!);
        Assert.Equal("R-1.04", (string)z["element"]!);
    }

    [Fact]
    public void Lesen_der_Nachrichten_des_PCs()
    {
        var w = KanalText.Lesen("{\"typ\":\"willkommen\",\"fassung\":1,\"id\":\"t0123abcd\",\"teilnehmer\":[],\"auftrag\":{\"modus\":\"begehen\",\"raum\":\"R-1\",\"nr\":3},\"stellschrauben\":{}}")!;
        Assert.Equal("willkommen", w.Value.Typ);
        var auftrag = VrStartLogik.AuftragPruefen(w.Value.Inhalt.GetProperty("auftrag"));
        Assert.Equal(new VrAuftrag("begehen", "R-1", null, false, null, 3, null), auftrag);
        Assert.Null(KanalText.Lesen("{\"typ\":\"auftrag\",\"fassung\":2}"));   // andere Fassung
        Assert.Null(KanalText.Lesen("{\"fassung\":1}"));
        Assert.Null(KanalText.Lesen("nicht json"));
        Assert.Null(KanalText.Lesen("[1]"));
    }

    // ---------------------------------------------------------------------------------------------- Umrechnung

    [Fact]
    public void Punkte_App_KICAD_mit_Bezugspunkt_hin_und_zurueck()
    {
        double[] b = [2_600_000, 1_200_000, 520];
        var k = KicadUmrechnung.PunktZuKicad(V(1.5, 1.7, -2.25), b);
        Assert.Equal([2_601_500, 1_202_250, 2220], k);   // x Ost; Nord = −z; oben = y
        var a = KicadUmrechnung.PunktAusKicad(k[0], k[1], k[2], b);
        Nah(a.X, 1.5); Nah(a.Y, 1.7); Nah(a.Z, -2.25);
        Assert.Equal([1500.0, 2250, 1700], KicadUmrechnung.PunktZuKicad(V(1.5, 1.7, -2.25), null));
        // dieselbe Abbildung wie der Kern beim Schreiben der GLB (XrKoordinaten.Punkt): gx = (x − bx)/1000, gy = (z − bz)/1000, gz = −(y − by)/1000
        var g = KicadUmrechnung.PunktAusKicad(2_603_000, 1_195_000, 3520, b);
        Nah(g.X, 3); Nah(g.Y, 3); Nah(g.Z, 5);
    }

    [Fact]
    public void Drehungen_App_KICAD_drehen_dieselben_Richtungen()
    {
        var zufall = new Random(7);
        for (int i = 0; i < 20; i++)
        {
            var q = Quaternion.Normalize(new Quaternion((float)zufall.NextDouble() - 0.5f, (float)zufall.NextDouble() - 0.5f, (float)zufall.NextDouble() - 0.5f, (float)zufall.NextDouble() - 0.5f));
            var v = new Vector3(0.3f, -0.8f, 0.52f);
            var appGedreht = Vector3.Transform(v, q);
            var kq = KicadUmrechnung.DrehungZuKicad(q);
            var kicadQ = new Quaternion((float)kq[0], (float)kq[1], (float)kq[2], (float)kq[3]);
            var vk = new Vector3(v.X, -v.Z, v.Y);   // App → KICAD (ohne Massstab)
            var kicadGedreht = Vector3.Transform(vk, kicadQ);
            Nah(kicadGedreht.X, appGedreht.X, 1e-5); Nah(kicadGedreht.Y, -appGedreht.Z, 1e-5); Nah(kicadGedreht.Z, appGedreht.Y, 1e-5);
            var zurueck = KicadUmrechnung.DrehungAusKicad(kq);
            Nah(Math.Abs(Quaternion.Dot(zurueck, q)), 1, 1e-6);
        }
        // Blick der App nach −z (Norden) mit dem Kopf um 90° nach links gedreht (Westen): in KICAD zeigt die Drehung (0, 1, 0) … nach −x
        var links = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        var kl = KicadUmrechnung.DrehungZuKicad(links);
        var nord = Vector3.Transform(new Vector3(0, 1, 0), new Quaternion((float)kl[0], (float)kl[1], (float)kl[2], (float)kl[3]));
        Nah(nord.X, -1, 1e-6); Nah(nord.Y, 0, 1e-6);
        Assert.Equal([0.0, 0, 0, 1], KicadUmrechnung.DrehungZuKicad(new Quaternion(0, 0, 0, 0)));
    }

    [Fact]
    public void Standort_relativ_absolut_und_Ende_mit_Bezugspunkt()
    {
        double[] b = [1000, 2000, 300];
        var s = StandortMit();   // Standpunkt (2000, 1500), Boden 250, Azimut 90
        var r = KicadUmrechnung.StandortRelativ(s, b)!;
        Assert.Equal(1000, r.Standpunkt!.X);
        Assert.Equal(-500, r.Standpunkt.Y);
        Assert.Equal(1400, r.Standpunkt.Z);
        Assert.Equal(-50, r.BodenOk);
        Assert.Equal(s, KicadUmrechnung.StandortAbsolut(r, b));
        var z = KicadUmrechnung.ZustandAusStandort(s, b)!;
        Nah(z.X, 1); Nah(z.Z, 0.5); Nah(z.Boden, -0.05);
        // Ende in der Szene → gemerkt in Projekt-mm (wie der Prototyp, dessen Szene keinen Bezugspunkt hat)
        var ende = KicadUmrechnung.EndeAbsolut(new StandpunktEnde(z.X, z.Z, z.Boden, z.Gieren), b);
        var g = VrStartLogik.StandpunktMerken(ende, s, "p1")!;
        Assert.Equal(2000, g.X); Assert.Equal(1500, g.Y); Assert.Equal(250, g.BodenOk); Assert.Equal(90, g.AzimutGrad);
        Assert.Null(KicadUmrechnung.StandortRelativ(null, b));
    }

    [Fact]
    public void Lesen_Auftrag_als_JsonElement_wie_vom_Kanal()
    {
        using var d = JsonDocument.Parse("{\"modus\":\"begehen\",\"raum\":\"R-1\",\"geschoss\":\"EG\",\"ausschnitt\":false,\"ansicht\":null,\"nr\":2,\"zeit\":\"2026-10-23T12:00:00Z\"}");
        var a = VrStartLogik.AuftragPruefen(d.RootElement)!;
        Assert.True(VrStartLogik.AuftragNeu(a, 1));
        Assert.False(VrStartLogik.AuftragNeu(a, 2));
        Assert.Equal([AuftragSchritt.Standort], VrStartLogik.AuftragSchritte(a));
    }
}
