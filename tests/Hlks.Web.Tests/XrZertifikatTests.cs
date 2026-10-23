using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Hlks.Web.Tests;

/// <summary>Zertifikat des XR-Zugangs (Quest App P3, XrZertifikat.cs): erzeugen, wiederverwenden, Fingerabdruck, Schutz, Ersatz; Einstellungen des Zugangs.</summary>
public class XrZertifikatTests
{
    static string NeuerOrdner() => Path.Combine(Path.GetTempPath(), "hlks_xrz_" + Guid.NewGuid().ToString("N")[..10]);
    static void Weg(string o) { try { if (Directory.Exists(o)) Directory.Delete(o, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }

    [Fact]
    public void Wird_erzeugt_und_wiederverwendet_der_Fingerabdruck_bleibt_gleich()
    {
        var o = NeuerOrdner();
        try
        {
            var e = new XrEinstellungen { Ordner = o };
            var a = new XrZertifikat(e);
            var z = a.Holen();
            Assert.True(a.NeuErzeugt);
            Assert.False(a.Ersetzt);
            Assert.True(z.HasPrivateKey);
            Assert.True(File.Exists(a.DateiPfad));
            Assert.StartsWith(Path.GetFullPath(o), a.DateiPfad);
            Assert.Matches("^[0-9a-f]{64}$", a.Fingerabdruck);
            Assert.Same(z, a.Holen());   // im selben Lauf dasselbe Objekt

            var b = new XrZertifikat(e);
            Assert.Equal(a.Fingerabdruck, b.Fingerabdruck);
            Assert.False(b.NeuErzeugt);
            Assert.True(b.Holen().HasPrivateKey);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(b.Holen().RawData)), b.Fingerabdruck);
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Ist_ECDSA_P256_nur_fuer_Serverauthentisierung_ohne_CA_zehn_Jahre()
    {
        var jetzt = DateTimeOffset.UtcNow;
        using var z = XrZertifikat.Neu("PC-TEST_1", jetzt);
        using var k = z.GetECDsaPublicKey();
        Assert.NotNull(k);
        Assert.Equal(256, k!.KeySize);
        Assert.Null(z.GetRSAPublicKey());
        Assert.Contains("CN=KICAD XR PC-TEST-1", z.Subject);
        var eku = Assert.Single(z.Extensions.OfType<X509EnhancedKeyUsageExtension>());
        Assert.Equal("1.3.6.1.5.5.7.3.1", Assert.Single(eku.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>()).Value);
        var bc = Assert.Single(z.Extensions.OfType<X509BasicConstraintsExtension>());
        Assert.False(bc.CertificateAuthority);
        Assert.InRange((z.NotAfter.ToUniversalTime() - jetzt.UtcDateTime).TotalDays, 3640, 3660);
        Assert.Equal("1.2.840.10045.4.3.2", z.SignatureAlgorithm.Value);   // ecdsa-with-SHA256
    }

    [Fact]
    public void Der_private_Schluessel_liegt_geschuetzt_auf_der_Platte()
    {
        var o = NeuerOrdner();
        try
        {
            var a = new XrZertifikat(new XrEinstellungen { Ordner = o });
            a.Holen();
            var roh = File.ReadAllBytes(a.DateiPfad);
            if (OperatingSystem.IsWindows())
            {
                Assert.EndsWith("zertifikat.dpapi", a.DateiPfad);
                // DPAPI: die Datei ist kein lesbares PKCS#12, erst XrSchutz.Oeffnen (nur dieser Benutzer auf diesem PC) macht es lesbar
                Assert.ThrowsAny<CryptographicException>(() => X509CertificateLoader.LoadPkcs12(roh, null));
                using var z = X509CertificateLoader.LoadPkcs12(XrSchutz.Oeffnen(roh), null);
                Assert.True(z.HasPrivateKey);
            }
            else
            {
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(a.DateiPfad));
            }
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Eine_unbrauchbare_Datei_wird_beiseitegelegt_und_ersetzt()
    {
        var o = NeuerOrdner();
        try
        {
            var e = new XrEinstellungen { Ordner = o };
            var pfad = new XrZertifikat(e).DateiPfad;
            Directory.CreateDirectory(o);
            File.WriteAllBytes(pfad, [1, 2, 3, 4, 5]);
            var a = new XrZertifikat(e);
            Assert.True(a.Holen().HasPrivateKey);
            Assert.True(a.Ersetzt);
            Assert.True(a.NeuErzeugt);
            Assert.Single(Directory.GetFiles(o, Path.GetFileName(pfad) + ".defekt-*"));
            Assert.Equal(a.Fingerabdruck, new XrZertifikat(e).Fingerabdruck);   // das neue gilt danach
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Kurz_vor_dem_Ablauf_entsteht_ein_neues()
    {
        var o = NeuerOrdner();
        try
        {
            var jetzt = DateTimeOffset.UtcNow;
            var alt = new XrZertifikat(new XrEinstellungen { Ordner = o, Uhr = () => jetzt }).Fingerabdruck;
            Assert.Equal(alt, new XrZertifikat(new XrEinstellungen { Ordner = o, Uhr = () => jetzt.AddDays(3000) }).Fingerabdruck);
            var spaeter = new XrZertifikat(new XrEinstellungen { Ordner = o, Uhr = () => jetzt.AddDays(3640) });
            Assert.NotEqual(alt, spaeter.Fingerabdruck);
            Assert.True(spaeter.Ersetzt);
        }
        finally { Weg(o); }
    }

    [Fact]
    public void Kurzform_fuer_den_Vergleich_durch_den_Menschen()
    {
        Assert.Equal("AB12 CD34 EF56 7890", XrZertifikat.Kurz("ab12cd34ef567890" + new string('0', 48)));
    }

    [Fact]
    public void Schutz_von_Texten_hin_und_zurueck()
    {
        var geheim = RandomNumberGenerator.GetBytes(32);
        var t = XrSchutz.TextSchuetzen(geheim);
        Assert.StartsWith(OperatingSystem.IsWindows() ? "dpapi:" : "klar:", t);
        Assert.Equal(geheim, XrSchutz.TextOeffnen(t));
        if (OperatingSystem.IsWindows()) Assert.DoesNotContain(Convert.ToBase64String(geheim), t);
        Assert.Null(XrSchutz.TextOeffnen("dpapi:kaputt!"));
        Assert.Null(XrSchutz.TextOeffnen("anders:AAAA"));
        Assert.Null(XrSchutz.TextOeffnen(null));
    }

    // ---------------------------------------------------------------- Einstellungen des Zugangs

    [Fact]
    public void Betrieb_alle_IPv4_Adressen_Ports_aus_Einstellung_nur_Desktop_Ordner_lokal()
    {
        var b = XrEinstellungen.Betrieb();
        Assert.Equal(IPAddress.Any, b.Adresse);
        Assert.Null(b.Port);
        Assert.Null(b.SuchPort);
        Assert.Equal(5130, b.DesktopPort);
        Assert.Equal(5140, XrEinstellungen.PortVorgabe);
        Assert.Equal(5141, XrEinstellungen.SuchPortVorgabe);
        var lokal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        Assert.Equal(Path.Combine(lokal, "HLKS_CAD", "xr"), b.OrdnerPfad());
        // im Testlauf: nur 127.0.0.1, freie Ports, Ordner unter %TEMP%
        var t = XrEinstellungen.Vorgabe();
        Assert.Equal(IPAddress.Loopback, t.Adresse);
        Assert.Equal(0, t.Port);
        Assert.Equal(0, t.SuchPort);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), t.OrdnerPfad());
    }

    [Fact]
    public void Benutzereinstellungen_xr_werden_geprueft()
    {
        BenutzerEinstellungen.Pruefen("xr.zugang", System.Text.Json.Nodes.JsonValue.Create(true));
        BenutzerEinstellungen.Pruefen("xr.port", System.Text.Json.Nodes.JsonValue.Create(5140));
        BenutzerEinstellungen.Pruefen("xr.suchport", System.Text.Json.Nodes.JsonValue.Create(65535));
        Assert.Throws<Hlks.Kern.HlksFehler>(() => BenutzerEinstellungen.Pruefen("xr.zugang", System.Text.Json.Nodes.JsonValue.Create("ja")));
        Assert.Throws<Hlks.Kern.HlksFehler>(() => BenutzerEinstellungen.Pruefen("xr.port", System.Text.Json.Nodes.JsonValue.Create(80)));
        Assert.Throws<Hlks.Kern.HlksFehler>(() => BenutzerEinstellungen.Pruefen("xr.suchport", System.Text.Json.Nodes.JsonValue.Create(70000)));
        Assert.Throws<Hlks.Kern.HlksFehler>(() => BenutzerEinstellungen.Pruefen("xr.port", System.Text.Json.Nodes.JsonValue.Create(5140.5)));
    }
}
