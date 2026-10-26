// Quest App P4a: Kopplungspaket lesen und ablegen, Pinning des Zertifikats, Ablage der App (Adressen, Auftragsnummer, gemerkter Standpunkt).

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using Hlks.Xr.Netz;

namespace Hlks.Xr.Tests;

internal static class NetzHilfen
{
    public static readonly byte[] Schluessel = [.. Enumerable.Range(1, 32).Select(i => (byte)i)];
    public static readonly string Fingerabdruck = Convert.ToHexStringLower(SHA256.HashData("kicad"u8));

    public static JsonObject PaketObjekt() => new()
    {
        ["art"] = "hlks-xr-kopplung", ["fassung"] = 1, ["pc"] = "PC-TEST", ["ips"] = new JsonArray("192.168.1.20", "10.0.0.5"), ["port"] = 5140, ["suchPort"] = 5141,
        ["fingerabdruck"] = Fingerabdruck, ["kopplung"] = "0123456789abcdef", ["schluessel"] = Base64Url.EncodeToString(Schluessel),
        ["brille"] = "1WMHH000X00000", ["name"] = "Quest Christian", ["angelegt"] = "2026-10-23T10:00:00+00:00",
    };

    public static Kopplungspaket Paket(Action<JsonObject>? aendern = null)
    {
        var o = PaketObjekt();
        aendern?.Invoke(o);
        return Kopplungspaket.Lesen(o.ToJsonString());
    }

    public static string Ordner()
    {
        var o = Path.Combine(Path.GetTempPath(), "hlks_xr_p4_" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(o);
        return o;
    }

    public static void Weg(string ordner)
    {
        try { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Selbst signiertes Zertifikat wie das von KICAD (ECDSA P-256).</summary>
    public static X509Certificate2 Zertifikat(string name = "KICAD XR TEST")
    {
        using var k = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var a = new CertificateRequest($"CN={name}", k, HashAlgorithmName.SHA256);
        return a.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }
}

public class NetzKopplungTests
{
    // ---------------------------------------------------------------------------------------------- Paket

    [Fact]
    public void Paket_wie_von_KICAD_wird_gelesen_auch_eingepackt()
    {
        var p = NetzHilfen.Paket();
        Assert.Equal("PC-TEST", p.Pc);
        Assert.Equal(["192.168.1.20", "10.0.0.5"], p.Ips);
        Assert.Equal(5140, p.Port);
        Assert.Equal(5141, p.SuchPort);
        Assert.Equal(NetzHilfen.Fingerabdruck, p.Fingerabdruck);
        Assert.Equal("0123456789abcdef", p.Kopplung);
        Assert.Equal("1WMHH000X00000", p.Brille);
        Assert.Equal("Quest Christian", p.Name);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 10, 0, 0, TimeSpan.Zero), p.Angelegt);
        Assert.Equal($"0123456789abcdef.{Base64Url.EncodeToString(NetzHilfen.Schluessel)}", p.Token);
        Assert.Equal(NetzHilfen.Schluessel, p.SchluesselBytes());
        // so liefert der Endpunkt es: { paket: { … } }
        var eingepackt = Kopplungspaket.Lesen(new JsonObject { ["paket"] = NetzHilfen.PaketObjekt() }.ToJsonString());
        Assert.Equal(p, eingepackt);
        // Fingerabdruck in Grossbuchstaben wird klein; Name fehlt → Kennung der Brille
        var q = NetzHilfen.Paket(o => { o["fingerabdruck"] = NetzHilfen.Fingerabdruck.ToUpperInvariant(); o.Remove("name"); o.Remove("angelegt"); });
        Assert.Equal(NetzHilfen.Fingerabdruck, q.Fingerabdruck);
        Assert.Equal("1WMHH000X00000", q.Name);
        Assert.Null(q.Angelegt);
        Assert.Equal(p.Fingerabdruck[..4].ToUpperInvariant() + " ", p.FingerabdruckKurz[..5]);
    }

    [Fact]
    public void Suchschluessel_ist_HMAC_des_Schluessels_wie_am_PC()
    {
        var p = NetzHilfen.Paket();
        Assert.Equal(HMACSHA256.HashData(NetzHilfen.Schluessel, Encoding.UTF8.GetBytes("hlks-xr-suche")), p.Suchschluessel());
    }

    [Fact]
    public void Der_Schluessel_erscheint_nicht_in_Texten()
    {
        var p = NetzHilfen.Paket();
        Assert.DoesNotContain(p.Schluessel, p.ToString());
        Assert.Contains("PC-TEST", p.ToString());
        Assert.Contains(p.Schluessel, p.AlsJson());   // nur in der Ablage
        Assert.Equal(p, Kopplungspaket.Lesen(p.AlsJson()));
    }

    [Theory]
    [InlineData("art", "\"anders\"", "art")]
    [InlineData("fassung", "3", "Fassung")]   // H+K XR E1: Fassung 1 und 2 gelten (KopplungspaketTests)
    [InlineData("fassung", "0", "Fassung")]
    [InlineData("pc", "\"\"", "pc")]
    [InlineData("ips", "[\"keine-ip\"]", "IPv4")]
    [InlineData("ips", "[\"fe80::1\"]", "IPv4")]
    [InlineData("ips", "\"192.168.1.20\"", "Liste")]
    [InlineData("port", "0", "port")]
    [InlineData("suchPort", "70000", "suchPort")]
    [InlineData("fingerabdruck", "\"abc\"", "fingerabdruck")]
    [InlineData("kopplung", "\"XYZ\"", "kopplung")]
    [InlineData("schluessel", "\"zu-kurz\"", "schluessel")]
    [InlineData("brille", "null", "brille")]
    public void Ungueltige_Felder_werden_genau_gemeldet(string feld, string wert, string erwartet)
    {
        var o = NetzHilfen.PaketObjekt();
        o[feld] = JsonNode.Parse(wert);
        var f = Assert.Throws<XrNetzFehler>(() => Kopplungspaket.Lesen(o.ToJsonString()));
        Assert.Contains(erwartet, f.Message);
        Assert.NotNull(f.Vorschlag);
    }

    [Fact]
    public void Kein_JSON_oder_kein_Objekt()
    {
        Assert.Throws<XrNetzFehler>(() => Kopplungspaket.Lesen("{"));
        Assert.Throws<XrNetzFehler>(() => Kopplungspaket.Lesen("[1]"));
    }

    [Fact]
    public void Ablage_Import_aus_dem_Eingang_in_den_privaten_Speicher_und_Loeschen()
    {
        var eingang = NetzHilfen.Ordner();
        var privat = NetzHilfen.Ordner();
        try
        {
            Assert.Null(KopplungAblage.Importieren(eingang, privat));   // nichts da
            Assert.Null(KopplungAblage.Laden(privat));
            File.WriteAllText(Path.Combine(eingang, "kopplung.json"), new JsonObject { ["paket"] = NetzHilfen.PaketObjekt() }.ToJsonString());
            Assert.Null(KopplungAblage.Importieren(eingang, privat));
            Assert.False(File.Exists(Path.Combine(eingang, "kopplung.json")));   // kein Schlüssel bleibt im geteilten Speicher
            Assert.Equal(NetzHilfen.Paket(), KopplungAblage.Laden(privat));
            // H+K XR E1: je Werkzeug eine Datei im privaten Speicher (das alte kopplung.json gilt als KICAD)
            var ablage = Path.Combine(privat, "kopplungen", "kicad.json");
            if (!OperatingSystem.IsWindows())
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(ablage));
            // ungültige Datei: Meldung, im Eingang gelöscht, die bisherige Kopplung bleibt
            File.WriteAllText(Path.Combine(eingang, "kopplung.json"), "{\"art\":\"x\"}");
            Assert.Contains("ungültig", KopplungAblage.Importieren(eingang, privat));
            Assert.False(File.Exists(Path.Combine(eingang, "kopplung.json")));
            Assert.Equal(NetzHilfen.Paket(), KopplungAblage.Laden(privat));
            // ungültige Ablage: Fehler beim Laden
            File.WriteAllText(ablage, "kaputt");
            Assert.Throws<XrNetzFehler>(() => KopplungAblage.Laden(privat));
            Assert.Null(KopplungAblage.Importieren(null, privat));
        }
        finally { NetzHilfen.Weg(eingang); NetzHilfen.Weg(privat); }
    }

    // ---------------------------------------------------------------------------------------------- Pinning

    [Fact]
    public void Pinning_nur_der_gepinnte_Fingerabdruck_gilt()
    {
        using var z = NetzHilfen.Zertifikat();
        using var anderes = NetzHilfen.Zertifikat("KICAD XR ANDERS");
        var fp = Convert.ToHexStringLower(SHA256.HashData(z.RawData));
        Assert.Equal(fp, XrPinning.Fingerabdruck(z));
        Assert.True(XrPinning.Passt(z, fp));
        Assert.True(XrPinning.Passt(z, fp.ToUpperInvariant()));
        Assert.False(XrPinning.Passt(anderes, fp));
        Assert.False(XrPinning.Passt(null, fp));
        Assert.False(XrPinning.Passt(z, ""));
        Assert.False(XrPinning.Passt(z, fp[..63]));
        // die Prüfung für TLS übergeht Fehler der Kette (selbst signiert) – nur der Pin zählt
        var pruefung = XrPinning.Pruefung(fp);
        Assert.True(pruefung(this, z, null, System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors | System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch));
        Assert.False(pruefung(this, anderes, null, System.Net.Security.SslPolicyErrors.None));
        using var h = XrPinning.Handler(fp);
        Assert.NotNull(h.SslOptions.RemoteCertificateValidationCallback);
        Assert.False(h.UseProxy);
    }

    // ---------------------------------------------------------------------------------------------- Ablage der App

    [Fact]
    public void AppAblage_Adressen_Auftragsnummer_Standpunkt_ueberstehen_den_Neustart()
    {
        var ordner = NetzHilfen.Ordner();
        try
        {
            var a = AppAblage.Laden(ordner);
            Assert.Empty(a.Adressen);
            for (int i = 1; i <= 6; i++) a.AdresseMerken($"192.168.1.{i}", 5140);
            a.AdresseMerken("192.168.1.4", 5140);
            Assert.Equal([("192.168.1.4", 5140), ("192.168.1.6", 5140), ("192.168.1.5", 5140), ("192.168.1.3", 5140)], a.Adressen);
            a.AuftragNr["0123456789abcdef"] = 7;
            a.Standpunkt = new GemerktStandpunkt { Ort = "p1", X = 1000, Y = -500, BodenOk = 250, AzimutGrad = 45, Raum = "R1", Geschoss = "EG", Zeit = "2026-10-23T12:00:00Z" };
            a.Speichern(ordner);
            var b = AppAblage.Laden(ordner);
            Assert.Equal(a.Adressen, b.Adressen);
            Assert.Equal(7, b.AuftragNr["0123456789abcdef"]);
            Assert.Equal(a.Standpunkt, b.Standpunkt);
            // gemerkter Standpunkt in derselben Form wie der Prototyp (vr.letzter_standpunkt)
            var k = AppAblage.StandpunktKnoten(a.Standpunkt);
            Assert.Equal(["ort", "x", "y", "bodenOk", "azimutGrad", "raum", "name", "nummer", "geschoss", "geschossName", "zeit"], k.Select(x => x.Key));
            File.WriteAllText(Path.Combine(ordner, AppAblage.Dateiname), "kaputt");
            Assert.Empty(AppAblage.Laden(ordner).Adressen);
        }
        finally { NetzHilfen.Weg(ordner); }
    }
}
